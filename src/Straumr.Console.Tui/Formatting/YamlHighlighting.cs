using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Formatting;

internal static class YamlHighlighting
{
    public static StyledRun[] Line(string line)
    {
        if (line.Length == 0)
        {
            return [];
        }

        List<StyledRun> runs = [];
        int index = Blanks(line, 0, runs);
        if (index >= line.Length)
        {
            return runs.ToArray();
        }

        if (line[index] == '#')
        {
            runs.Add(new StyledRun(index, line.Length - index, StraumrStyleService.CodeNote));
            return runs.ToArray();
        }

        if (line.AsSpan(index).StartsWith("---") || line.AsSpan(index).StartsWith("..."))
        {
            runs.Add(new StyledRun(index, 3, StraumrStyleService.CodePunctuation));
            index = Blanks(line, index + 3, runs);
        }

        while (index < line.Length && line[index] == '-' && (index + 1 == line.Length || line[index + 1] is ' ' or '\t'))
        {
            runs.Add(new StyledRun(index, 1, StraumrStyleService.CodePunctuation));
            index = Blanks(line, index + 1, runs);
        }

        int key = KeyEnd(line, index);
        if (key > index)
        {
            runs.Add(new StyledRun(index, key - index, StraumrStyleService.CodeKey));
            runs.Add(new StyledRun(key, 1, StraumrStyleService.CodePunctuation));
            index = Blanks(line, key + 1, runs);
        }

        Value(line, index, runs);
        return runs.ToArray();
    }

    private static void Value(string line, int index, List<StyledRun> runs)
    {
        while (index < line.Length)
        {
            char character = line[index];
            if (char.IsWhiteSpace(character))
            {
                int start = index;
                index = Blanks(line, index, null);
                if (index < line.Length && line[index] == '#')
                {
                    runs.Add(new StyledRun(start, line.Length - start, StraumrStyleService.CodeNote));
                    return;
                }

                runs.Add(new StyledRun(start, index - start, StraumrStyleService.CodePlain));
                continue;
            }

            if (character is '"' or '\'')
            {
                int start = index;
                index = EndOfQuoted(line, index);
                runs.Add(new StyledRun(start, index - start, StraumrStyleService.CodeString));
                continue;
            }

            if (IsPunctuation(character))
            {
                runs.Add(new StyledRun(index, 1, StraumrStyleService.CodePunctuation));
                index++;
                continue;
            }

            int token = index;
            while (index < line.Length && !IsBreak(line[index]))
            {
                index++;
            }

            runs.Add(new StyledRun(token, index - token, Word(line.AsSpan(token, index - token))));
        }
    }

    private static int KeyEnd(string line, int index)
    {
        for (int scan = index; scan < line.Length; scan++)
        {
            char character = line[scan];
            if (character is '"' or '\'' or '#')
            {
                return -1;
            }

            if (character == ':' && (scan + 1 == line.Length || line[scan + 1] is ' ' or '\t'))
            {
                return scan;
            }
        }

        return -1;
    }

    private static int Blanks(string line, int index, List<StyledRun>? runs)
    {
        int start = index;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
        {
            index++;
        }

        if (runs is not null && index > start)
        {
            runs.Add(new StyledRun(start, index - start, StraumrStyleService.CodePlain));
        }

        return index;
    }

    private static Style Word(ReadOnlySpan<char> word) =>
        IsBoolean(word) ? StraumrStyleService.CodeBoolean
        : IsNull(word) ? StraumrStyleService.CodeNull
        : IsNumber(word) ? StraumrStyleService.CodeNumber
        : word[0] is '&' or '*' or '!' ? StraumrStyleService.CodeNote
        : StraumrStyleService.CodePlain;

    private static bool IsBoolean(ReadOnlySpan<char> word) =>
        word.Equals("true", StringComparison.OrdinalIgnoreCase) || word.Equals("false", StringComparison.OrdinalIgnoreCase)
        || word.Equals("yes", StringComparison.OrdinalIgnoreCase) || word.Equals("no", StringComparison.OrdinalIgnoreCase)
        || word.Equals("on", StringComparison.OrdinalIgnoreCase) || word.Equals("off", StringComparison.OrdinalIgnoreCase);

    private static bool IsNull(ReadOnlySpan<char> word) =>
        word.SequenceEqual("~") || word.Equals("null", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumber(ReadOnlySpan<char> word)
    {
        bool digits = false;
        foreach (char character in word)
        {
            if (char.IsAsciiDigit(character))
            {
                digits = true;
                continue;
            }

            if (character is not ('-' or '+' or '.' or 'e' or 'E'))
            {
                return false;
            }
        }

        return digits;
    }

    private static bool IsPunctuation(char character) =>
        character is '[' or ']' or '{' or '}' or ',' or ':' or '|' or '>';

    private static bool IsBreak(char character) =>
        char.IsWhiteSpace(character) || character is '"' or '\'' || IsPunctuation(character);

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
