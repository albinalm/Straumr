namespace Straumr.Console.Tui.Formatting;

internal static class CountFormatting
{
    /// <summary>Renders a count as <c>no requests</c>, <c>1 request</c> or <c>18 requests</c>.</summary>
    public static string Label(int count, string noun) =>
        count == 0
            ? $"no {noun}s"
            : $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
}
