using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Formatting;

internal static class FormHighlighting
{
    public static StyledRun[] Line(string line)
    {
        if (line.Length == 0)
        {
            return [];
        }

        List<StyledRun> runs = [];
        int index = 0;
        bool naming = true;
        while (index < line.Length)
        {
            char character = line[index];
            if (IsSeparator(character))
            {
                runs.Add(new StyledRun(index, 1, StraumrStyleService.CodePunctuation));
                naming = character != '=';
                index++;
                continue;
            }

            int start = index;
            while (index < line.Length && !IsSeparator(line[index]))
            {
                index++;
            }

            runs.Add(new StyledRun(start, index - start,
                naming ? StraumrStyleService.CodeKey : StraumrStyleService.CodeString));
        }

        return runs.ToArray();
    }

    private static bool IsSeparator(char character) => character is '&' or '=' or ';';
}
