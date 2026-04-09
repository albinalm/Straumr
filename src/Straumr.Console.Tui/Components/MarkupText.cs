using System.Text.RegularExpressions;

namespace Straumr.Console.Tui.Components;

internal static partial class MarkupText
{
    public static string ToPlain(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return MarkupRegex().Replace(value, string.Empty);
    }

    [GeneratedRegex(@"\[[^\[\]]+\]")]
    private static partial Regex MarkupRegex();
}
