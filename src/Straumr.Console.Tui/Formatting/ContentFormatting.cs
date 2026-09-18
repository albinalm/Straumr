using System.Text;
using System.Text.Json;

namespace Straumr.Console.Tui.Formatting;

internal static class ContentFormatting
{
    private const int PreviewLimit = 64 * 1024;

    public const string Truncated = "… preview truncated at 64 KiB of text.";

    public static string Preview(string? content, bool formatJson = false)
    {
        if (string.IsNullOrEmpty(content))
            return "No body.";
        if (content.Length > PreviewLimit)
            return content[..PreviewLimit] + "\n" + Truncated;
        if (!formatJson)
            return content;
        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                document.WriteTo(writer);
            return Preview(Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch (JsonException)
        {
            return content;
        }
    }

    public static string Fields(IEnumerable<KeyValuePair<string, string>> fields, string empty) =>
        Join(fields.Select(field => $"{field.Key}: {field.Value}"), empty);

    public static string Headers(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
        Join(headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}"), "No headers.");

    private static string Join(IEnumerable<string> lines, string empty)
    {
        string text = string.Join('\n', lines);
        return text.Length == 0 ? empty : Preview(text);
    }

    public static string Unsaved(long bytes) =>
        $"This body was not saved: {Size(bytes)} is above the response store limit.\nPress s to send the request again.";

    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KiB",
        _ => $"{bytes / (1024d * 1024):0.#} MiB"
    };
}
