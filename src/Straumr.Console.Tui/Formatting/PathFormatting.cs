namespace Straumr.Console.Tui.Formatting;

internal static class PathFormatting
{
    public static string Display(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? $"~{path[home.Length..]}"
            : path;
    }
}
