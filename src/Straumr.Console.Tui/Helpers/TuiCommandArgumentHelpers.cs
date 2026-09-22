using System.Text;

namespace Straumr.Console.Tui.Helpers;

public static class TuiCommandArgumentHelpers
{
    public static bool TryParseSingle(string argument, out string value, out string? error)
    {
        string text = argument.Trim();
        value = string.Empty;
        error = null;
        if (text.Length == 0)
        {
            return true;
        }

        if (text[0] != '"')
        {
            if (text.Any(char.IsWhiteSpace))
            {
                error = "names containing spaces must be wrapped in double quotes";
                return false;
            }

            value = text;
            return true;
        }

        var parsed = new StringBuilder();
        for (int index = 1; index < text.Length; index++)
        {
            char current = text[index];
            if (current == '"')
            {
                if (text[(index + 1)..].Trim().Length > 0)
                {
                    error = "unexpected text after the closing double quote";
                    return false;
                }

                value = parsed.ToString();
                return true;
            }

            if (current == '\\' && index + 1 < text.Length && text[index + 1] is '\\' or '"')
            {
                current = text[++index];
            }

            parsed.Append(current);
        }

        error = "missing closing double quote";
        return false;
    }
}
