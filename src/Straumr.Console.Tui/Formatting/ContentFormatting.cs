namespace Straumr.Console.Tui.Formatting;

internal static class ContentFormatting
{
    private const int PreviewLimit = 64 * 1024;

    public const string Truncated = "… preview truncated at 64 KiB of text.";

    public static string Preview(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "No body.";
        }

        return content.Length > PreviewLimit ? content[..PreviewLimit] + "\n" + Truncated : content;
    }

    public static string Fields(IEnumerable<KeyValuePair<string, string>> fields, string empty) =>
        Join(fields.Select(field => $"{field.Key}: {field.Value}"), empty);

    private static string Join(IEnumerable<string> lines, string empty)
    {
        string text = string.Join('\n', lines);
        return text.Length == 0 ? empty : Preview(text);
    }

    public static string Unsaved(long bytes, string sendAction = "Request.Send") =>
        $"This body was not saved: {Size(bytes)} is above the response store limit.\nPress {TuiKeybindHelpers.Hint(sendAction)} to send the request again.";

    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KiB",
        _ => $"{bytes / (1024d * 1024):0.#} MiB"
    };
}
