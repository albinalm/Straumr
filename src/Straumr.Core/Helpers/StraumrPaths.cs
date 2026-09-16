namespace Straumr.Core.Helpers;

/// <summary>
/// Expands the shorthands a hand-written path may use.
/// </summary>
/// <remarks>
/// Paths in the settings file are typed by a person, so they carry what a person types: a leading
/// <c>~</c> and environment variables. Nothing else reads a path written by hand, which is why this
/// is not applied to paths the program stored itself.
/// </remarks>
public static class StraumrPaths
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

        // A path typed with forward slashes and joined to a Windows home reads back with both kinds
        // in it, and this one is shown to the reader. Only a rooted path is normalised: resolving a
        // relative one would silently anchor it to whatever directory the app happens to be in.
        if (!Path.IsPathRooted(expanded))
            return expanded;

        try
        {
            return Path.GetFullPath(expanded);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return expanded;
        }
    }

    /// <summary>Expands a path that may not have been written at all.</summary>
    public static string? ExpandOrNull(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Expand(path);
}
