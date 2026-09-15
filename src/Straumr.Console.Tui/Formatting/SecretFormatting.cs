using Straumr.Core.Helpers;

namespace Straumr.Console.Tui.Formatting;

/// <summary>
/// How a secret reference reads where a value is shown rather than edited.
/// </summary>
internal static class SecretFormatting
{
    /// <summary>
    /// Shortens every reference in <paramref name="text"/> for display: <c>{{secret:token}}</c>
    /// reads as <c>{token}</c>.
    /// </summary>
    /// <remarks>
    /// The stored form is deliberately unmistakable, which is right in a document being written and
    /// wrong in a list of names: four braces and a keyword around a short name is most of a row
    /// spent on syntax, and a name that ends in one is trimmed away before the reader reaches what
    /// it refers to. One pair of braces keeps it reading as a placeholder at half the width.
    /// This is for labels only — a name, a URL, a summary. What the editor and the body preview
    /// show is the text as it is stored, because that is the text being edited.
    /// </remarks>
    public static string Display(string? text) =>
        string.IsNullOrEmpty(text)
            ? string.Empty
            : SecretHelpers.SecretPattern.Replace(text, match => $"{{{match.Groups["name"].Value}}}");
}
