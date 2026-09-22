using System.Text.RegularExpressions;

namespace Straumr.Core.Helpers;

public static class SecretHelpers
{
    public static readonly Regex SecretPattern = new("\\{\\{secret:(?<name>[^}]+)\\}\\}", RegexOptions.Compiled);
}
