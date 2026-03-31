using System.Text;
using System.Text.RegularExpressions;
using Humanizer;

namespace Straumr.Core.Extensions;

public static partial class StringExtensions
{
    public static string ToFileName(this string name)
    {
        return GetFileName(name);
    }
    
    private static string GetFileName(string name)
    {
        ReadOnlySpan<char> invalidChars = ['/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0', '!'];
    
        string kebaberized = name.Kebaberize().ToLowerInvariant();
    
        var sb = new StringBuilder(kebaberized.Length);
        foreach (char c in kebaberized)
        {
            if (invalidChars.Contains(c) || char.IsControl(c))
            {
                sb.Append('-');
            }
            else
            {
                sb.Append(c);
            }
        }

        string result = CollapseHyphensRegex().Replace(sb.ToString(), "-").Trim('-');

        return string.IsNullOrEmpty(result) ? "unnamed" : result;
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex CollapseHyphensRegex();
}