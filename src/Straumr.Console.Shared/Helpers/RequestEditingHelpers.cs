using System.Diagnostics;
using Straumr.Core.Enums;
using Straumr.Core.Models;
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
}
