using System.Text.RegularExpressions;

namespace Straumr.Core.Helpers;

public static class VariableHelpers
{
    public const string SecretPrefix = "secret:";

    public static readonly Regex ReferencePattern = new(@"\{\{(?<name>[^{}]+)\}\}", RegexOptions.Compiled);

    public static readonly Regex VariablePattern = new(@"\{\{(?!\s*secret:)(?<name>[^{}]+)\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsSecretName(string name) =>
        name.TrimStart().StartsWith(SecretPrefix, StringComparison.OrdinalIgnoreCase);

    public static string StripSecretPrefix(string name) =>
        name.TrimStart()[SecretPrefix.Length..].Trim();
}
