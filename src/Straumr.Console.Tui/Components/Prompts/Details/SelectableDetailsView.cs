using System.Text;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.Prompts.Details;

internal sealed class SelectableDetailsView : View
{
    private readonly IReadOnlyList<(string Key, string Value)> _rows;
    private readonly StraumrTheme? _theme;
    private int _topRow;
    private bool _isSelecting;
    private (int Row, int Col)? _selectionStart;
    private (int Row, int Col)? _selectionEnd;
    private int _cachedWidth = -1;
    private List<List<StyledCell>> _renderedLines = [];

    public SelectableDetailsView(IReadOnlyList<(string Key, string Value)> rows, StraumrTheme? theme)
    {
        _rows = rows;
        _theme = theme;
        CanFocus = true;
        MousePositionTracking = true;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        BuildLinesIfNeeded();

        ClearViewport(context);

        int height = Viewport.Height;
        int width = Viewport.Width;
        for (int y = 0; y < height; y++)
        {
            int rowIndex = _topRow + y;
            if (rowIndex >= _renderedLines.Count)
            {
                break;
            }

            List<StyledCell> line = _renderedLines[rowIndex];
            for (int x = 0; x < width; x++)
            {
                StyledCell cell = x < line.Count ? line[x] : new StyledCell(' ', BaseAttribute);
                SetAttribute(IsSelected(rowIndex, x) ? SelectionAttribute : cell.Attribute);
                AddRune(x, y, new Rune(cell.Char));
            }
        }

        return true;
    }

    protected override bool OnKeyDown(Key key)
    {
        BuildLinesIfNeeded();

        if (key == Key.C.WithCtrl)
        {
            CopySelection();
            return true;
        }

        if (key == Key.A.WithCtrl)
        {
            SelectAll();
            return true;
        }

        if (key == Key.PageDown)
        {
            _topRow = Math.Min(Math.Max(0, _renderedLines.Count - 1), _topRow + Math.Max(1, Viewport.Height - 1));
            SetNeedsDraw();
            return true;
        }

        if (key == Key.PageUp)
        {
            _topRow = Math.Max(0, _topRow - Math.Max(1, Viewport.Height - 1));
            SetNeedsDraw();
            return true;
        }

        if (key == Key.CursorDown)
        {
            _topRow = Math.Min(Math.Max(0, _renderedLines.Count - 1), _topRow + 1);
            SetNeedsDraw();
            return true;
        }

        if (key == Key.CursorUp)
        {
            _topRow = Math.Max(0, _topRow - 1);
            SetNeedsDraw();
            return true;
        }

        return base.OnKeyDown(key);
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        BuildLinesIfNeeded();

        if (mouse.Position is not { } position)
        {
            return base.OnMouseEvent(mouse);
        }

        int row = Math.Clamp(position.Y + _topRow, 0, Math.Max(0, _renderedLines.Count - 1));
        int col = Math.Max(0, position.X);

        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonPressed))
        {
            if (!_isSelecting)
            {
                App?.Mouse.GrabMouse(this);
                _isSelecting = true;
                _selectionStart = (row, col);
                SetFocus();
            }

            _selectionEnd = (row, col);
            SetNeedsDraw();
            return true;
        }

        if (_isSelecting && mouse.Flags.HasFlag(MouseFlags.PositionReport))
        {
            _selectionEnd = (row, col);
            SetNeedsDraw();
            return true;
        }

        if (_isSelecting && (mouse.Flags.HasFlag(MouseFlags.LeftButtonReleased) || mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked)))
        {
            App?.Mouse.UngrabMouse();
            _isSelecting = false;
            if ((row, col) == _selectionStart)
            {
                _selectionStart = null;
                _selectionEnd = null;
            }
            else
            {
                _selectionEnd = (row, col);
            }
            SetNeedsDraw();
            return true;
        }

        if (mouse.Flags.HasFlag(MouseFlags.WheeledDown))
        {
            _topRow = Math.Min(Math.Max(0, _renderedLines.Count - 1), _topRow + 3);
            SetNeedsDraw();
            return true;
        }

        if (mouse.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            _topRow = Math.Max(0, _topRow - 3);
            SetNeedsDraw();
            return true;
        }

        return base.OnMouseEvent(mouse);
    }

    public int ComputeLineCount(int width) => BuildRenderedLines(Math.Max(1, width)).Count;

    private void BuildLinesIfNeeded()
    {
        int width = Math.Max(1, Viewport.Width);
        if (_cachedWidth == width && _renderedLines.Count > 0)
        {
            return;
        }

        _cachedWidth = width;
        _renderedLines = BuildRenderedLines(width);
        _topRow = Math.Clamp(_topRow, 0, Math.Max(0, _renderedLines.Count - 1));
    }

    private List<List<StyledCell>> BuildRenderedLines(int width)
    {
        int keyWidth = Math.Clamp(_rows.Max(r => r.Key.Length), 8, 24);
        int valueWidth = Math.Max(8, width - keyWidth - 2);

        List<List<StyledCell>> lines = [];
        foreach ((string Key, string Value) row in _rows)
        {
            List<StyledRun> runs = ParseRuns(row.Value);
            List<List<StyledCell>> wrappedValueLines = WrapRuns(runs, valueWidth);
            if (wrappedValueLines.Count == 0)
            {
                wrappedValueLines.Add([]);
            }

            for (int i = 0; i < wrappedValueLines.Count; i++)
            {
                List<StyledCell> line = [];
                string prefix = i == 0 ? row.Key.PadRight(keyWidth) : new string(' ', keyWidth);
                line.AddRange(ToCells(prefix, BaseAttribute));
                line.AddRange(ToCells("  ", BaseAttribute));
                line.AddRange(wrappedValueLines[i]);
                lines.Add(line);
            }
        }

        return lines;
    }

    private List<List<StyledCell>> WrapRuns(List<StyledRun> runs, int width)
    {
        List<List<StyledCell>> lines = [[]];
        int col = 0;

        foreach (StyledRun run in runs)
        {
            foreach (char ch in run.Text)
            {
                if (ch == '\n')
                {
                    lines.Add([]);
                    col = 0;
                    continue;
                }

                if (col >= width)
                {
                    lines.Add([]);
                    col = 0;
                }

                lines[^1].Add(new StyledCell(ch, run.Attribute));
                col++;
            }
        }

        return lines;
    }

    private List<StyledRun> ParseRuns(string? value)
    {
        string text = value ?? string.Empty;
        List<StyledRun> runs = [];
        var styleStack = new Stack<StyleState>();
        StyleState currentStyle = StyleState.Default(_theme);
        var buffer = new StringBuilder();

        int index = 0;
        while (index < text.Length)
        {
            if (text[index] != '[')
            {
                buffer.Append(text[index]);
                index++;
                continue;
            }

            int tagEnd = text.IndexOf(']', index);
            if (tagEnd < 0)
            {
                buffer.Append(text[index]);
                index++;
                continue;
            }

            string tag = text[(index + 1)..tagEnd];
            FlushBuffer();

            if (tag == "/")
            {
                currentStyle = styleStack.Count > 0 ? styleStack.Pop() : StyleState.Default(_theme);
            }
            else if (!TryApplyTag(tag, currentStyle, out StyleState nextStyle))
            {
                buffer.Append(text[index..(tagEnd + 1)]);
            }
            else
            {
                styleStack.Push(currentStyle);
                currentStyle = nextStyle;
            }

            index = tagEnd + 1;
        }

        FlushBuffer();
        return runs;

        void FlushBuffer()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            runs.Add(new StyledRun(buffer.ToString(), currentStyle.ToAttribute()));
            buffer.Clear();
        }
    }

    private bool TryApplyTag(string tag, StyleState currentStyle, out StyleState nextStyle)
    {
        string? color = tag.ToLowerInvariant() switch
        {
            "grey" or "gray" or "secondary" => _theme?.Secondary,
            "success" => _theme?.Success,
            "info" => _theme?.Info,
            "warning" => _theme?.Warning,
            "danger" => _theme?.Danger,
            "primary" => _theme?.Primary,
            "accent" => _theme?.Accent,
            "surface" => _theme?.OnSurface,
            _ => null,
        };

        if (tag.Equals("bold", StringComparison.OrdinalIgnoreCase))
        {
            nextStyle = currentStyle with { Style = currentStyle.Style | TextStyle.Bold };
            return true;
        }

        if (color is null)
        {
            nextStyle = currentStyle;
            return false;
        }

        nextStyle = currentStyle with { Foreground = color };
        return true;
    }

    private void SelectAll()
    {
        if (_renderedLines.Count == 0)
        {
            return;
        }

        int lastRow = _renderedLines.Count - 1;
        int lastCol = Math.Max(0, _renderedLines[lastRow].Count - 1);
        _selectionStart = (0, 0);
        _selectionEnd = (lastRow, lastCol);
        SetNeedsDraw();
    }

    private void CopySelection()
    {
        string selected = GetSelectedText();
        if (string.IsNullOrEmpty(selected))
        {
            return;
        }

        try
        {
#pragma warning disable CS0618
            Application.Clipboard?.TrySetClipboardData(selected);
#pragma warning restore CS0618
        }
        catch
        {
            // Ignore clipboard failures; selection remains available visually.
        }
    }

    private string GetSelectedText()
    {
        if (_selectionStart is null || _selectionEnd is null)
        {
            return string.Empty;
        }

        ((int Row, int Col) start, (int Row, int Col) end) = NormalizeSelection(_selectionStart.Value, _selectionEnd.Value);
        var builder = new StringBuilder();
        for (int row = start.Row; row <= end.Row; row++)
        {
            List<StyledCell> line = _renderedLines[row];
            int startCol = row == start.Row ? Math.Min(start.Col, line.Count) : 0;
            int endCol = row == end.Row ? Math.Min(end.Col, Math.Max(0, line.Count - 1)) : Math.Max(0, line.Count - 1);

            for (int col = startCol; col <= endCol && col < line.Count; col++)
            {
                builder.Append(line[col].Char);
            }

            if (row < end.Row)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private bool IsSelected(int row, int col)
    {
        if (_selectionStart is null || _selectionEnd is null)
        {
            return false;
        }

        ((int Row, int Col) start, (int Row, int Col) end) = NormalizeSelection(_selectionStart.Value, _selectionEnd.Value);
        if (row < start.Row || row > end.Row)
        {
            return false;
        }

        int startCol = row == start.Row ? start.Col : 0;
        int endCol = row == end.Row ? end.Col : int.MaxValue;
        return col >= startCol && col <= endCol;
    }

    private static ((int Row, int Col) Start, (int Row, int Col) End) NormalizeSelection((int Row, int Col) a, (int Row, int Col) b)
    {
        if (a.Row > b.Row || (a.Row == b.Row && a.Col > b.Col))
        {
            return (b, a);
        }

        return (a, b);
    }

    private List<StyledCell> ToCells(string text, Attribute attribute)
    {
        List<StyledCell> cells = new(text.Length);
        foreach (char ch in text)
        {
            cells.Add(new StyledCell(ch, attribute));
        }

        return cells;
    }

    private Attribute BaseAttribute => new(
        ColorResolver.Resolve(_theme?.OnSurface ?? "White"),
        ColorResolver.Resolve(_theme?.Surface ?? "Black"));

    private Attribute SecondaryAttribute => new(
        ColorResolver.Resolve(_theme?.Secondary ?? "Gray"),
        ColorResolver.Resolve(_theme?.Surface ?? "Black"));

    private Attribute SelectionAttribute => new(
        ColorResolver.Resolve(_theme?.OnPrimary ?? "Black"),
        ColorResolver.Resolve(_theme?.Primary ?? "BrightGreen"));

    private sealed record StyledRun(string Text, Attribute Attribute);
    private sealed record StyledCell(char Char, Attribute Attribute);
    private sealed record StyleState(string Foreground, string Background, TextStyle Style)
    {
        public static StyleState Default(StraumrTheme? theme)
            => new(theme?.OnSurface ?? "White", theme?.Surface ?? "Black", TextStyle.None);

        public Attribute ToAttribute()
            => new(ColorResolver.Resolve(Foreground), ColorResolver.Resolve(Background), Style);
    }
}
