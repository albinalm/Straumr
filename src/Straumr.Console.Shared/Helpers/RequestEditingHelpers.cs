using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Straumr.Core.Enums;
using Straumr.Core.Helpers;

namespace Straumr.Console.Shared.Helpers;

public static class RequestEditingHelpers
{
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

    /// <summary>
    /// Whether a URL is one a request can be built from. A secret reference stands where a value
    /// will be, so it is replaced by a placeholder before parsing: <c>{{secret:host}}</c> is not a
    /// valid URL component, and refusing it would refuse the whole point of references.
    /// </summary>
    public static bool IsValidAbsoluteUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = SecretHelpers.SecretPattern.Replace(value, "secret");
        return Uri.TryCreate(normalized, UriKind.Absolute, out _);
    }

    /// <summary>The HTTP methods a request can be sent with, in the order they are offered.</summary>
    public static readonly string[] HttpMethods =
        ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT"];

    /// <summary>The body types, and the display names the CLI and the TUI both call them by.</summary>
    public static readonly BodyType[] BodyTypes =
    [
        BodyType.None, BodyType.Json, BodyType.Xml, BodyType.Text,
        BodyType.FormUrlEncoded, BodyType.MultipartForm, BodyType.Raw
    ];

    /// <summary>
    /// The <c>Content-Type</c> a body type implies, or <see langword="null"/> where it implies none.
    /// Setting a body type sets this header, because a body the server cannot identify is a body the
    /// request did not really carry.
    /// </summary>
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

    /// <summary>Applies <see cref="ContentType"/> to a header map, removing it where there is none.</summary>
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

    /// <summary>
    /// What an external editor is opened on for a body of this type: a document to start from when
    /// there is no body yet, and otherwise the body itself.
    /// </summary>
    /// <remarks>
    /// An empty file is a poor thing to be handed. A body that does not exist yet is about to be
    /// written in a language whose first and last characters are known, so they are written for the
    /// reader along with the indentation the line between them will need.
    /// A body kept on one line is one a program wrote rather than a person, and opening an editor is
    /// exactly the moment to lay it out; a body that already has lines is left exactly as it is,
    /// because its layout is then someone's decision and not an accident.
    /// </remarks>
    public static EditorDocument BodyEditingDocument(BodyType type, string body)
    {
        if (body.Length == 0)
        {
            // The caret goes where the writing starts, which for a scaffold is not the first
            // character of the file: inside the braces, past the indentation, or under the
            // declaration the document has to be written below.
            return type switch
            {
                BodyType.Json => new EditorDocument(
                    $"{{{Environment.NewLine}  {Environment.NewLine}}}{Environment.NewLine}", 2, 3),
                BodyType.Xml => new EditorDocument(
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?>{Environment.NewLine}", 2, 1),
                _ => EditorDocument.AtStart(string.Empty)
            };
        }

        if (type != BodyType.Json || body.Contains('\n'))
        {
            return AtContinuation(body, type == BodyType.Json);
        }

        return AtContinuation(
            TryFormatJson(body, indented: true, out string? formatted) ? formatted : body,
            json: true);
    }

    /// <summary>
    /// Where the caret belongs in a document that already has something in it: at the end of the
    /// line the reader would carry on from.
    /// </summary>
    /// <remarks>
    /// For most documents that is the last line with anything on it, which is where writing stopped.
    /// JSON ends on a brace instead, and the end of a closing brace is the one place in the document
    /// nothing can be added — the next member goes on the line above it, so that is the line the
    /// caret opens on. Only brackets and their commas are skipped, so a document whose last value
    /// sits on the same line as its closing brace keeps the caret on that value.
    /// </remarks>
    private static EditorDocument AtContinuation(string text, bool json)
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

        return new EditorDocument(text, index + 1, lines[index].Length + 1);
    }

    /// <summary>
    /// Lays JSON out over lines or collapses it onto one. Returns <see langword="false"/> for text
    /// that is not JSON, which is not a failure — only an answer the caller has to decide about.
    /// </summary>
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

    /// <summary>
    /// The extension a body of this type is written out under when it is handed to an external
    /// editor, leading dot included. It is the only thing that tells the editor which language it has
    /// been given, and so which highlighting and indentation to apply to it.
    /// </summary>
    /// <remarks>
    /// A field body never reaches here: its fields are edited as fields, and there is no document to
    /// open. Raw is plain text because that is exactly what it claims to be.
    /// </remarks>
    public static string BodyTypeFileExtension(BodyType type)
    {
        return type switch
        {
            BodyType.Json => ".json",
            BodyType.Xml => ".xml",
            _ => ".txt"
        };
    }

    /// <summary>
    /// Whether a body of this type is one document rather than a set of named fields. A form and a
    /// multipart form are edited as the fields they are; everything else is edited as text.
    /// </summary>
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

/// <summary>
/// A document on its way to an external editor, and where the caret belongs in it.
/// </summary>
/// <param name="Line">The 1-based line the editor should open on.</param>
/// <param name="Column">The 1-based column on that line.</param>
/// <remarks>
/// The position is a request and not a guarantee. There is no universal way to ask an editor to
/// open somewhere: each one that can has a flag of its own, and one that cannot is opened at the
/// top, which is where it would have opened anyway.
/// </remarks>
public readonly record struct EditorDocument(string Text, int Line, int Column)
{
    public static EditorDocument AtStart(string text) => new(text, 1, 1);

    /// <summary>Whether the caret is anywhere but where the editor would put it unasked.</summary>
    public bool HasPosition => Line > 1 || Column > 1;
}
