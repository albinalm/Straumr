using System.Text.Json;
using System.Text.RegularExpressions;
using Straumr.Core.Exceptions;

namespace Straumr.Core.Helpers;

public static class ResponseValueExtractor
{
    public static string Extract(string body, IReadOnlyDictionary<string, IEnumerable<string>> headers,
        ExtractionSource source, string expression, bool requireScalar = true)
    {
        try
        {
            string value = source switch
            {
                ExtractionSource.JsonPath => FromJson(body, expression, requireScalar),
                ExtractionSource.ResponseHeader => headers.FirstOrDefault(pair =>
                    pair.Key.Equals(expression, StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault()
                    ?? throw new StraumrException("The extraction response header was not found", StraumrError.InvalidEntry),
                ExtractionSource.Regex => FromRegex(body, expression),
                _ => throw new StraumrException("Unsupported response extraction source", StraumrError.InvalidEntry)
            };
            if (requireScalar && string.IsNullOrWhiteSpace(value))
            {
                throw new StraumrException("Response extraction returned an empty value", StraumrError.InvalidEntry);
            }
            return value;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or RegexMatchTimeoutException)
        {
            throw new StraumrException("Response extraction failed: check the response format and extraction expression", StraumrError.InvalidEntry, exception);
        }
    }
    public static void Validate(ExtractionSource source, string expression)
    {
        if (!Enum.IsDefined(source) || string.IsNullOrWhiteSpace(expression))
        {
            throw new StraumrException("An extraction source and expression are required", StraumrError.InvalidEntry);
        }
        if (source != ExtractionSource.Regex)
        {
            return;
        }
        try
        {
            _ = new Regex(expression, RegexOptions.None, TimeSpan.FromSeconds(2));
        }
        catch (ArgumentException exception)
        {
            throw new StraumrException("Invalid extraction regex", StraumrError.InvalidEntry, exception);
        }
    }
    private static string FromJson(string json, string path, bool requireScalar)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement current = document.RootElement;
        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out int index) && index >= 0 && index < current.GetArrayLength())
            {
                current = current[index];
            }
            else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out JsonElement next))
            {
                current = next;
            }
            else
            {
                throw new StraumrException("The JSON extraction path could not be resolved", StraumrError.InvalidEntry);
            }
        }
        if (requireScalar && current.ValueKind is JsonValueKind.Null or JsonValueKind.Object or JsonValueKind.Array)
        {
            throw new StraumrException("Response extraction requires a non-null scalar value", StraumrError.InvalidEntry);
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() ?? string.Empty : current.GetRawText();
    }
    private static string FromRegex(string body, string pattern)
    {
        Match match = Regex.Match(body, pattern, RegexOptions.None, TimeSpan.FromSeconds(2));
        return match.Success ? match.Groups.Count > 1 ? match.Groups[1].Value : match.Value
            : throw new StraumrException("Response extraction regex did not match", StraumrError.InvalidEntry);
    }
}
