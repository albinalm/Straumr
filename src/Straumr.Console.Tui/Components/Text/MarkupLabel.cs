using System.Text;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.Text;

internal sealed class MarkupLabel : View
{
    private List<List<StyledCell>> _lines = [];
    private int _cachedWidth = -1;
    private string _cachedMarkup = string.Empty;
    private StraumrTheme? _cachedTheme;
    private string _markup = string.Empty;
    private StraumrTheme? _theme;

    private IReadOnlyList<IReadOnlyList<MarkupText.MarkupRun>>? _parsedLines;
    private int _plainMaxLineLength;
    private string? _parsedCacheMarkup;
    private StraumrTheme? _parsedCacheTheme;

    public string Markup
    {
        get => _markup;
        set
        {
            string next = value ?? string.Empty;
            if (_markup == next)
            {
                return;
            }

            _markup = next;
            _parsedLines = null;
            SetNeedsDraw();
        }
    }

    public StraumrTheme? Theme
    {
        get => _theme;
        set
        {
            if (ReferenceEquals(_theme, value))
            {
                return;
            }

            _theme = value;
            _parsedLines = null;
            SetNeedsDraw();
        }
    }

    internal IReadOnlyList<IReadOnlyList<MarkupText.MarkupRun>> ParsedLines
    {
        get
        {
            EnsureParsedCache();
            return _parsedLines!;
        }
    }

    internal int PlainMaxLineLength
    {
        get
        {
            EnsureParsedCache();
            return _plainMaxLineLength;
        }
    }

    private void EnsureParsedCache()
    {
        if (_parsedLines is not null
            && _parsedCacheMarkup == _markup
            && ReferenceEquals(_parsedCacheTheme, _theme))
        {
            return;
        }

        string[] rawLines = _markup.Split('\n');
        var parsed = new List<IReadOnlyList<MarkupText.MarkupRun>>(rawLines.Length);
        int maxLen = 0;
        foreach (string rawLine in rawLines)
        {
            List<MarkupText.MarkupRun> runs = MarkupText.ParseRuns(rawLine, _theme);
            parsed.Add(runs);

            int lineLen = 0;
            foreach (MarkupText.MarkupRun run in runs)
            {
                lineLen += run.Text.Length;
            }

            if (lineLen > maxLen)
            {
                maxLen = lineLen;
            }
        }

        _parsedLines = parsed;
        _plainMaxLineLength = maxLen;
        _parsedCacheMarkup = _markup;
        _parsedCacheTheme = _theme;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        BuildLinesIfNeeded();
        ClearViewport(context);

        int height = Viewport.Height;
        int width = Viewport.Width;

        for (int y = 0; y < height && y < _lines.Count; y++)
        {
            List<StyledCell> line = _lines[y];
            int maxX = Math.Min(width, line.Count);
            for (int x = 0; x < maxX; x++)
            {
                StyledCell cell = line[x];
                SetAttribute(cell.Attribute);
                AddRune(x, y, new Rune(cell.Char));
            }
        }

        return true;
    }

    private void BuildLinesIfNeeded()
    {
        int width = Math.Max(1, Viewport.Width);
        if (_cachedWidth == width && _cachedMarkup == Markup && ReferenceEquals(_cachedTheme, Theme))
        {
            return;
        }

        _cachedWidth = width;
        _cachedMarkup = Markup ?? string.Empty;
        _cachedTheme = Theme;

        List<MarkupText.MarkupRun> runs = MarkupText.ParseRuns(_cachedMarkup, Theme);
        _lines = WrapRuns(runs, width);
    }

    private static List<List<StyledCell>> WrapRuns(List<MarkupText.MarkupRun> runs, int width)
    {
        List<List<StyledCell>> lines = [[]];
        int col = 0;

        foreach (MarkupText.MarkupRun run in runs)
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

        return lines.Count == 0 ? [[]] : lines;
    }

    private sealed record StyledCell(char Char, Attribute Attribute);
}
