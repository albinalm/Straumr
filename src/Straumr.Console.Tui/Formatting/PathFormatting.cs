namespace Straumr.Console.Tui.Formatting;

internal static class PathFormatting
{
    /// <summary>
    /// Replaces the user's home directory with <c>~</c>. A path is usually the widest value a region
    /// has to carry, and the part that identifies it is the tail, so shortening the head is what keeps
    /// the tail on screen.
    /// </summary>
    public static string Display(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? $"~{path[home.Length..]}"
            : path;
    }
}
