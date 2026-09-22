namespace Straumr.Console.Tui.Formatting;

internal static class CountFormatting
{
    public static string Label(int count, string noun) =>
        count == 0
            ? $"no {noun}s"
            : $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
}
