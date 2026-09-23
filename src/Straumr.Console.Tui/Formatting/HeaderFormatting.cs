using Straumr.Core.Helpers;

namespace Straumr.Console.Tui.Formatting;

internal static class HeaderFormatting
{
    public const string NotRecorded = "Not recorded. Sent headers are not kept with the request; send it again to see them.";

    public static IReadOnlyList<KeyValuePair<string, string>> Rows(
        IEnumerable<KeyValuePair<string, string>> headers) =>
        headers.OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase).ToList();

    public static IReadOnlyList<KeyValuePair<string, string>> Rows(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
        Rows(headers.Select(header =>
            new KeyValuePair<string, string>(header.Key, string.Join(", ", header.Value))));

    public static IReadOnlyList<KeyValuePair<string, string>> Sent(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> sent,
        IReadOnlyDictionary<string, string> configured) =>
        Rows(sent.Select(header => new KeyValuePair<string, string>(header.Key,
            Template(header.Key, configured) ?? string.Join(", ", header.Value))));

    private static string? Template(string name, IReadOnlyDictionary<string, string> configured) =>
        configured.FirstOrDefault(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value is
            { } value && SecretHelpers.SecretPattern.IsMatch(value)
            ? value
            : null;

    public static string? ContentType(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
        headers.Where(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            .SelectMany(header => header.Value)
            .FirstOrDefault();

    public static string? ContentType(IEnumerable<KeyValuePair<string, string>> headers) =>
        headers.Where(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value)
            .FirstOrDefault();
}
