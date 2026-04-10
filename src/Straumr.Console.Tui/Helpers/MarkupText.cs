using System.Text;
using System.Text.RegularExpressions;

using Straumr.Console.Shared.Theme;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Helpers;

internal static partial class MarkupText
{
    public sealed record MarkupRun(string Text, Attribute Attribute);

    public static string ToPlain(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return MarkupRegex().Replace(value, string.Empty);
    }

    public static List<List<Cell>> ToLinesOfCells(string? value, StraumrTheme? theme)
    {
        string text = value ?? string.Empty;
        if (text.Length == 0)
        {
            return [[]];
        }

        Attribute defaultAttribute = BuildAttribute(theme?.OnSurface, theme?.Surface);
        Attribute currentAttribute = defaultAttribute;

        List<List<Cell>> lines = [];
        int index = 0;

        while (index < text.Length)
        {
            int tagStart = text.IndexOf('[', index);
            if (tagStart < 0)
            {
                AppendSegment(lines, text[index..], currentAttribute);
                break;
            }

            if (tagStart > index)
            {
                AppendSegment(lines, text[index..tagStart], currentAttribute);
            }

            int tagEnd = text.IndexOf(']', tagStart);
            if (tagEnd < 0)
            {
                AppendSegment(lines, text[tagStart..], currentAttribute);
                break;
            }

            string tag = text[(tagStart + 1)..tagEnd];
            if (tag == "/")
            {
                currentAttribute = defaultAttribute;
            }
            else if (TryResolveTagAttribute(tag, theme, out Attribute resolved))
            {
                currentAttribute = resolved;
            }
            else
            {
                AppendSegment(lines, text[tagStart..(tagEnd + 1)], currentAttribute);
            }

            index = tagEnd + 1;
        }

        return lines;
    }

    public static List<MarkupRun> ParseRuns(string? value, StraumrTheme? theme)
    {
        string text = value ?? string.Empty;
        List<MarkupRun> runs = [];
        var styleStack = new Stack<StyleState>();
        StyleState currentStyle = StyleState.Default(theme);
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
                currentStyle = styleStack.Count > 0 ? styleStack.Pop() : StyleState.Default(theme);
            }
            else if (!TryApplyTag(tag, theme, currentStyle, out StyleState nextStyle))
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

            runs.Add(new MarkupRun(buffer.ToString(), currentStyle.ToAttribute()));
            buffer.Clear();
        }
    }

    private static void AppendSegment(List<List<Cell>> lines, string segment, Attribute attribute)
    {
        if (lines.Count == 0)
        {
            lines.Add([]);
        }

        string[] split = segment.Split('\n');
        for (var i = 0; i < split.Length; i++)
        {
            lines[^1].AddRange(Cell.ToCellList(split[i], attribute));
            if (i < split.Length - 1)
            {
                lines.Add([]);
            }
        }
    }

    private static bool TryResolveTagAttribute(string tag, StraumrTheme? theme, out Attribute attribute)
    {
        string? color = tag.ToLowerInvariant() switch
        {
            "grey" or "gray" or "secondary" => theme?.Secondary,
            "success" => theme?.Success,
            "info" => theme?.Info,
            "warning" => theme?.Warning,
            "danger" => theme?.Danger,
            "primary" => theme?.Primary,
            "accent" => theme?.Accent,
            "surface" => theme?.OnSurface,
            "bold" => theme?.OnSurface,
            _ => null,
        };

        if (color is null)
        {
            attribute = default;
            return false;
        }

        attribute = BuildAttribute(color, theme?.Surface);
        return true;
    }

    private static Attribute BuildAttribute(string? foreground, string? background)
    {
        Color fg = ColorResolver.Resolve(foreground ?? "White");
        Color bg = ColorResolver.Resolve(background ?? "Black");
        return new Attribute(fg, bg);
    }

    private static bool TryApplyTag(string tag, StraumrTheme? theme, StyleState currentStyle, out StyleState nextStyle)
    {
        string? color = tag.ToLowerInvariant() switch
        {
            "grey" or "gray" or "secondary" => theme?.Secondary,
            "success" => theme?.Success,
            "info" => theme?.Info,
            "warning" => theme?.Warning,
            "danger" => theme?.Danger,
            "primary" => theme?.Primary,
            "accent" => theme?.Accent,
            "surface" => theme?.OnSurface,
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

    private sealed record StyleState(string Foreground, string Background, TextStyle Style)
    {
        public static StyleState Default(StraumrTheme? theme)
            => new(theme?.OnSurface ?? "White", theme?.Surface ?? "Black", TextStyle.None);

        public Attribute ToAttribute()
            => new(ColorResolver.Resolve(Foreground), ColorResolver.Resolve(Background), Style);
    }

    [GeneratedRegex(@"\[[^\[\]]+\]")]
    private static partial Regex MarkupRegex();
}
