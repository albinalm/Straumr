using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Straumr.Core.Enums;
using Straumr.Core.Helpers;

namespace Straumr.Console.Shared.Helpers;

public static class RequestEditingHelpers
{

    public static readonly string[] HttpMethods =
        ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT"];

    public static readonly BodyType[] BodyTypes =
    [
        BodyType.None, BodyType.Json, BodyType.Xml, BodyType.Text,
        BodyType.FormUrlEncoded, BodyType.MultipartForm, BodyType.Raw
    ];
    public static string BodyTypeDisplayName(BodyType type)
    {
        return type switch
        {
            BodyType.None => "None",
            BodyType.Json => "JSON",
            BodyType.Xml => "XML",
            BodyType.Text => "Text",
            BodyType.FormUrlEncoded => "Form URL Encoded",
            BodyType.MultipartForm => "Multipart Form",
            BodyType.Raw => "Raw",
            _ => type.ToString()
        };
    }

    public static bool IsValidAbsoluteUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = SecretHelpers.SecretPattern.Replace(value, "secret");
        return Uri.TryCreate(normalized, UriKind.Absolute, out _);
    }

    public static string? ContentType(BodyType type)
    {
        return type switch
        {
            BodyType.Json => "application/json",
            BodyType.Xml => "application/xml",
            BodyType.Text => "text/plain",
            BodyType.FormUrlEncoded => "application/x-www-form-urlencoded",
            BodyType.MultipartForm => "multipart/form-data",
            _ => null
        };
    }

    public static void SyncContentTypeHeader(IDictionary<string, string> headers, BodyType type)
    {
        if (ContentType(type) is { } contentType)
        {
            headers["Content-Type"] = contentType;
        }
        else
        {
            headers.Remove("Content-Type");
        }
    }

    public static EditorDocumentModel BodyEditingDocument(BodyType type, string body)
    {
        if (body.Length == 0)
        {
            return type switch
            {
                BodyType.Json => new EditorDocumentModel(
                    $"{{{Environment.NewLine}  {Environment.NewLine}}}{Environment.NewLine}", 2, 3),
                BodyType.Xml => new EditorDocumentModel(
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?>{Environment.NewLine}", 2, 1),
                _ => EditorDocumentModel.AtStart(string.Empty)
            };
        }

        if (type != BodyType.Json || body.Contains('\n'))
        {
            return AtContinuation(body, type == BodyType.Json);
        }

        return AtContinuation(
            TryFormatJson(body, true, out string? formatted) ? formatted : body,
            true);
    }

    private static EditorDocumentModel AtContinuation(string text, bool json)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');

        int index = lines.Length - 1;
        while (index > 0 && lines[index].Trim().Length == 0)
        {
            index--;
        }

        if (json && index > 0 && lines[index].Trim().All(character => character is '}' or ']' or ','))
        {
            int inside = index - 1;
            while (inside > 0 && lines[inside].Trim().Length == 0)
            {
                inside--;
            }

            index = inside;
        }

        return new EditorDocumentModel(text, index + 1, lines[index].Length + 1);
    }

    public static bool TryFormatJson(string? text, bool indented, [NotNullWhen(true)] out string? formatted)
    {
        formatted = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented }))
            {
                document.WriteTo(writer);
            }

            formatted = Encoding.UTF8.GetString(stream.ToArray());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool IsJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string BodyTypeFileExtension(BodyType type)
    {
        return type switch
        {
            BodyType.Json => ".json",
            BodyType.Xml => ".xml",
            _ => ".txt"
        };
    }

    public static bool IsFieldBody(BodyType type) =>
        type is BodyType.FormUrlEncoded or BodyType.MultipartForm;

    public static string EscapeFormFieldComponent(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        List<string> placeholders = new();
        string protectedValue = SecretHelpers.SecretPattern.Replace(value, match =>
        {
            string token = $"__STRAUMR_SECRET_{placeholders.Count}__";
            placeholders.Add(match.Value);
            return token;
        });

        string escaped = Uri.EscapeDataString(protectedValue);
        for (int i = 0; i < placeholders.Count; i++)
        {
            string token = Uri.EscapeDataString($"__STRAUMR_SECRET_{i}__");
            escaped = escaped.Replace(token, placeholders[i], StringComparison.Ordinal);
        }

        return escaped;
    }

    public static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return string.Join("&",
            parameters.Select(kv =>
                $"{EscapeFormFieldComponent(kv.Key)}={EscapeFormFieldComponent(kv.Value)}"));
    }

    public static Dictionary<string, string> ParseQueryString(string? query, StringComparer? comparer = null)
    {
        Dictionary<string, string> result = new(comparer ?? StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            string key = Uri.UnescapeDataString(pair[..separatorIndex]);
            string value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            result[key] = value;
        }

        return result;
    }
}
