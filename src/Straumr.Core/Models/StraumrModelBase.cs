using System.Text;
using System.Text.RegularExpressions;
using Humanizer;

namespace Straumr.Core.Models;

public partial class StraumrModelBase
{
    public string Id => GetFileName(Name);
    public required string Name { get; set; }
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;
    
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