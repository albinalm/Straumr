using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Formatting;

internal static class XmlHighlighting
{
    public static StyledRun[] Line(string line)
    {
        if (line.Length == 0)
        {
            return [];
        }

        List<StyledRun> runs = [];
        int index = 0;
        while (index < line.Length)
        {
            if (line[index] == '<')
            {
                index = Markup(line, index, runs);
                continue;
            }

            int start = index;
            while (index < line.Length && line[index] != '<')
            {
                index++;
            }

            runs.Add(new StyledRun(start, index - start, StraumrStyleService.CodePlain));
        }

        return runs.ToArray();
    }

    private static int Markup(string line, int index, List<StyledRun> runs)
    {
        if (line.AsSpan(index).StartsWith("<!--"))
        {
            int close = line.IndexOf("-->", index, StringComparison.Ordinal);
            return Note(line, index, close < 0 ? line.Length : close + 3, runs);
        }

        if (line.AsSpan(index).StartsWith("<!") || line.AsSpan(index).StartsWith("<?"))
        {
            int close = line.IndexOf('>', index);
            return Note(line, index, close < 0 ? line.Length : close + 1, runs);
        }

        int open = index + (line.AsSpan(index).StartsWith("</") ? 2 : 1);
        runs.Add(new StyledRun(index, open - index, StraumrStyleService.CodePunctuation));
        index = Name(line, open, StraumrStyleService.CodeKey, runs);
        while (index < line.Length && line[index] != '>')
        {
            index = Attribute(line, index, runs);
        }

        if (index >= line.Length)
        {
            return index;
        }

        runs.Add(new StyledRun(index, 1, StraumrStyleService.CodePunctuation));
        return index + 1;
    }

    private static int Attribute(string line, int index, List<StyledRun> runs)
    {
        char character = line[index];
        if (char.IsWhiteSpace(character))
        {
            int start = index;
            while (index < line.Length && char.IsWhiteSpace(line[index]))
            {
                index++;
            }

            runs.Add(new StyledRun(start, index - start, StraumrStyleService.CodePlain));
            return index;
        }

        if (character is '=' or '/')
        {
            runs.Add(new StyledRun(index, 1, StraumrStyleService.CodePunctuation));
            return index + 1;
        }

        if (character is '"' or '\'')
        {
            int start = index;
            index = EndOfQuoted(line, index);
            runs.Add(new StyledRun(start, index - start, StraumrStyleService.CodeString));
            return index;
        }

        int named = Name(line, index, StraumrStyleService.CodeBoolean, runs);
        if (named > index)
        {
            return named;
        }

        runs.Add(new StyledRun(index, 1, StraumrStyleService.CodePlain));
        return index + 1;
    }

    private static int Name(string line, int index, Style style, List<StyledRun> runs)
    {
        int start = index;
        while (index < line.Length && !IsDelimiter(line[index]))
        {
            index++;
        }

        if (index > start)
        {
            runs.Add(new StyledRun(start, index - start, style));
        }

        return index;
    }

    private static int Note(string line, int index, int end, List<StyledRun> runs)
    {
        runs.Add(new StyledRun(index, end - index, StraumrStyleService.CodeNote));
        return end;
    }

    private static bool IsDelimiter(char character) =>
        char.IsWhiteSpace(character) || character is '<' or '>' or '/' or '=' or '"' or '\'';

    private static int EndOfQuoted(string line, int index)
    {
        char quote = line[index];
        index++;
        while (index < line.Length)
        {
            if (line[index] == quote)
            {
                return index + 1;
            }

            index++;
        }

        return line.Length;
    }
}
