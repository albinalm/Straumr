using Straumr.Core.Helpers;

namespace Straumr.Console.Tui.Formatting;

internal static class SecretFormatting
{
    public static string Display(string? text) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : SecretHelpers.SecretPattern.Replace(text, match => $"{{{match.Groups["name"].Value}}}");
}
