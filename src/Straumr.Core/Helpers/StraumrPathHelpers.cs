namespace Straumr.Core.Helpers;

public static class StraumrPathHelpers
{
    public static string Expand(string path)
    {
        string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (expanded.Length > 0 && expanded[0] == '~')
        {
            expanded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                expanded[1..].TrimStart('/', '\\'));
        }

        if (!Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        try
        {
            return Path.GetFullPath(expanded);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return expanded;
        }
    }

    public static string? ExpandOrNull(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Expand(path);
}
