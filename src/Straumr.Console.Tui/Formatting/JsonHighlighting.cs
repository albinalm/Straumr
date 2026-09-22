using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Formatting;

internal static class JsonHighlighting
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
            int start = index;
            Style style;
            char character = line[index];
            if (character == '"')
            {
                index = EndOfString(line, index);
                style = Names(line, index) ? StraumrStyleService.CodeKey : StraumrStyleService.CodeString;
            }
            else if (character is '-' or '+' || char.IsAsciiDigit(character))
            {
                index++;
                while (index < line.Length && IsNumberPart(line[index]))
                {
                    index++;
                }

                style = StraumrStyleService.CodeNumber;
            }
            else if (char.IsAsciiLetter(character))
            {
                index++;
                while (index < line.Length && char.IsAsciiLetter(line[index]))
                {
                    index++;
                }

                style = Word(line.AsSpan(start, index - start));
            }
            else if (IsPunctuation(character))
            {
                index++;
                style = StraumrStyleService.CodePunctuation;
            }
            else
            {
                index++;
                while (index < line.Length && !StartsToken(line[index]))
                {
                    index++;
                }

                style = StraumrStyleService.CodePlain;
            }

            runs.Add(new StyledRun(start, index - start, style));
        }

        return runs.ToArray();
    }

    private static Style Word(ReadOnlySpan<char> word) =>
        word.SequenceEqual("true") || word.SequenceEqual("false") ? StraumrStyleService.CodeBoolean
        : word.SequenceEqual("null") ? StraumrStyleService.CodeNull
        : StraumrStyleService.CodePlain;

    private static bool Names(string line, int index)
    {
        while (index < line.Length && char.IsWhiteSpace(line[index]))
        {
            index++;
        }

        return index < line.Length && line[index] == ':';
    }

    private static int EndOfString(string line, int index)
    {
        index++;
        while (index < line.Length)
        {
            if (line[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (line[index] == '"')
            {
                return index + 1;
            }

            index++;
        }

        return line.Length;
    }

    private static bool IsNumberPart(char character) =>
        char.IsAsciiDigit(character) || character is '-' or '+' or '.' or 'e' or 'E';

    private static bool IsPunctuation(char character) =>
        character is '{' or '}' or '[' or ']' or ':' or ',';

    private static bool StartsToken(char character) =>
        character is '"' or '-' or '+' || char.IsAsciiLetterOrDigit(character) || IsPunctuation(character);
}
