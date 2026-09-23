using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Straumr.Core.Enums;

namespace Straumr.Console.Shared.Helpers;

public static class ContentFormatHelpers
{
    public static ContentLanguage Detect(string? contentType, string? content) =>
        FromMediaType(contentType) ?? Sniff(content);

    public static ContentLanguage FromBodyType(BodyType type) => type switch
    {
        BodyType.Json => ContentLanguage.Json,
        BodyType.Xml => ContentLanguage.Xml,
        BodyType.FormUrlEncoded => ContentLanguage.FormUrlEncoded,
        _ => ContentLanguage.PlainText
    };

    public static bool CanFormat(ContentLanguage language) =>
        language is ContentLanguage.Json or ContentLanguage.Xml or ContentLanguage.Html;

    public static string DisplayName(ContentLanguage language) => language switch
    {
        ContentLanguage.Json => "JSON",
        ContentLanguage.Xml => "XML",
        ContentLanguage.Html => "HTML",
        ContentLanguage.Yaml => "YAML",
        ContentLanguage.FormUrlEncoded => "form data",
        _ => "text"
    };

    public static string FormattableNames() => "JSON, XML and HTML";

    public static bool TryFormat(ContentLanguage language, string? text, bool indented, [NotNullWhen(true)] out string? formatted)
    {
        formatted = null;
        return language switch
        {
            ContentLanguage.Json => RequestEditingHelpers.TryFormatJson(text, indented, out formatted),
            ContentLanguage.Xml or ContentLanguage.Html => TryFormatXml(text, indented, out formatted),
            _ => false
        };
    }

    public static bool IsXml(string? text) => TryParseXml(text, out _);

    private static ContentLanguage? FromMediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        ReadOnlySpan<char> media = contentType.AsSpan();
        int separator = media.IndexOf(';');
        media = (separator < 0 ? media : media[..separator]).Trim();

        return Suffixed(media, "json") ? ContentLanguage.Json
            : Suffixed(media, "html") ? ContentLanguage.Html
            : Suffixed(media, "xml") ? ContentLanguage.Xml
            : Suffixed(media, "yaml") || Suffixed(media, "yml") ? ContentLanguage.Yaml
            : media.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) ? ContentLanguage.FormUrlEncoded
            : null;
    }

    private static bool Suffixed(ReadOnlySpan<char> media, ReadOnlySpan<char> token) =>
        media.EndsWith(token, StringComparison.OrdinalIgnoreCase) &&
        media.Length > token.Length && media[media.Length - token.Length - 1] is '/' or '+' or '-';

    private static ContentLanguage Sniff(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ContentLanguage.PlainText;
        }

        string trimmed = content.Trim();
        if (trimmed[0] is '{' or '[' && RequestEditingHelpers.IsJson(trimmed))
        {
            return ContentLanguage.Json;
        }

        if (trimmed[0] == '<')
        {
            return IsXml(trimmed) ? ContentLanguage.Xml : ContentLanguage.Html;
        }

        return LooksFormEncoded(trimmed) ? ContentLanguage.FormUrlEncoded
            : LooksYaml(trimmed) ? ContentLanguage.Yaml
            : ContentLanguage.PlainText;
    }

    private static bool LooksFormEncoded(string text)
    {
        if (!text.Contains('='))
        {
            return false;
        }

        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character) || character is '{' or '}' or '<' or '>' or '"')
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksYaml(string text)
    {
        int entries = 0;
        foreach (string line in text.Replace("\r", string.Empty).Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            if (trimmed is "---" or "...")
            {
                return true;
            }

            if (!IsYamlEntry(trimmed))
            {
                return false;
            }

            if (++entries > 1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsYamlEntry(string line)
    {
        if (line.StartsWith("- ", StringComparison.Ordinal))
        {
            return true;
        }

        int colon = line.IndexOf(':');
        return colon > 0 && !line.AsSpan(0, colon).ContainsAny('#', '"', '\'')
               && (colon + 1 == line.Length || line[colon + 1] is ' ' or '\t');
    }

    private static bool TryFormatXml(string? text, bool indented, out string? formatted)
    {
        formatted = null;
        if (!TryParseXml(text, out XDocument? document))
        {
            return false;
        }

        var builder = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = indented,
            IndentChars = "  ",
            OmitXmlDeclaration = true,
            NewLineChars = "\n"
        };
        using (XmlWriter writer = XmlWriter.Create(builder, settings))
        {
            document!.Save(writer);
        }

        string body = builder.ToString();
        formatted = document!.Declaration is { } declaration
            ? indented ? $"{declaration}\n{body}" : $"{declaration}{body}"
            : body;
        return true;
    }

    private static bool TryParseXml(string? text, out XDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            document = XDocument.Parse(text);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
