using Straumr.Core.Models;

namespace Straumr.Console.Tui.Screens.Workspace;

public sealed record WorkspaceScreenItem(
    StraumrWorkspace Workspace,
    StraumrWorkspaceEntry Entry,
    bool IsCurrent)
{
    public string ShortId => Workspace.Id.ToString("N")[..8];

    public string DisplayPath => Shorten(Entry.Path);

    public string DisplayDirectory =>
        Shorten(Path.GetDirectoryName(Entry.Path) ?? Entry.Path);

    private static string Shorten(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? $"~{path[home.Length..]}"
            : path;
    }
}
