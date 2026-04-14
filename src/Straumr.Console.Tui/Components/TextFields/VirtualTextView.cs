using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.TextFields;

internal sealed class VirtualTextView : View
{
    private string _text = string.Empty;
    private List<string> _wrappedLines = [string.Empty];
    private int _cachedWidth = -1;
    private int _topLine;
    private Attribute _attribute;

    public VirtualTextView()
    {
        CanFocus = true;
    }

    public event EventHandler? ScrollStateChanged;

    public new string Text
    {
        get => _text;
        set
        {
            _text = value;
            _cachedWidth = -1;
            _topLine = 0;
            SetNeedsDraw();
            ScrollStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int TotalLines
    {
        get
        {
            EnsureWrapped();
            return _wrappedLines.Count;
        }
    }

    public int TopLine => _topLine;

    public int VisibleLines => Math.Max(0, Viewport.Height);

    public void ApplyTheme(Color foreground, Color background)
    {
        _attribute = new Attribute(foreground, background);
        SetScheme(new Scheme(_attribute) { Focus = _attribute });
        SetNeedsDraw();
    }

    public void ScrollLines(int delta)
    {
        EnsureWrapped();
        int max = Math.Max(0, _wrappedLines.Count - VisibleLines);
        int next = Math.Clamp(_topLine + delta, 0, max);
        if (next == _topLine)
        {
            return;
        }

        _topLine = next;
        SetNeedsDraw();
        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollToStart()
    {
        if (_topLine == 0)
        {
            return;
        }

        _topLine = 0;
        SetNeedsDraw();
        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollToEnd()
    {
        EnsureWrapped();
        int max = Math.Max(0, _wrappedLines.Count - VisibleLines);
        if (_topLine == max)
        {
            return;
        }

        _topLine = max;
        SetNeedsDraw();
        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetTopLine(int value)
    {
        EnsureWrapped();
        int max = Math.Max(0, _wrappedLines.Count - VisibleLines);
        int clamped = Math.Clamp(value, 0, max);
        if (clamped == _topLine)
        {
            return;
        }

        _topLine = clamped;
        SetNeedsDraw();
        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        EnsureWrapped();
        ClearViewport(context);

        int width = Viewport.Width;
        int height = Viewport.Height;

        SetAttribute(_attribute);
        for (var y = 0; y < height; y++)
        {
            int lineIndex = _topLine + y;
            if (lineIndex >= _wrappedLines.Count)
            {
                break;
            }

            string line = _wrappedLines[lineIndex];
            int maxX = Math.Min(width, line.Length);
            for (var x = 0; x < maxX; x++)
            {
                AddRune(x, y, new Rune(line[x]));
            }
        }

        return true;
    }

    private void EnsureWrapped()
    {
        int width = Math.Max(1, Viewport.Width);
        if (width == _cachedWidth)
        {
            return;
        }

        _cachedWidth = width;
        _wrappedLines = WrapText(_text, width);
        int max = Math.Max(0, _wrappedLines.Count - VisibleLines);
        _topLine = Math.Clamp(_topLine, 0, max);
        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static List<string> WrapText(string text, int width)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            lines.Add(string.Empty);
            return lines;
        }

        foreach (string raw in text.Split('\n'))
        {
            if (raw.Length <= width)
            {
                lines.Add(raw);
                continue;
            }

            var pos = 0;
            while (pos < raw.Length)
            {
                int remaining = raw.Length - pos;
                if (remaining <= width)
                {
                    lines.Add(raw[pos..]);
                    break;
                }

                int windowEnd = pos + width;
                int breakAt = -1;
                for (int k = windowEnd; k > pos; k--)
                {
                    if (raw[k - 1] == ' ')
                    {
                        breakAt = k;
                        break;
                    }
                }

                if (breakAt <= pos)
                {
                    breakAt = windowEnd;
                }

                lines.Add(raw[pos..breakAt].TrimEnd());
                pos = breakAt;
                while (pos < raw.Length && raw[pos] == ' ')
                {
                    pos++;
                }
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(string.Empty);
        }

        return lines;
    }
}
