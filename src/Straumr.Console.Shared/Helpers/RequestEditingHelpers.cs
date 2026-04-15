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
