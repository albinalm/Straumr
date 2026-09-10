using Straumr.Core.Models;

namespace Straumr.Console.Tui.Screens.Workspace;

/// <remarks>
/// Which workspace is current is deliberately not a field here. It changes while the screen is live,
/// so the screen holds it as state and the row is derived from that; a value baked in at load would
/// go stale the moment a workspace was activated.
/// </remarks>
public sealed record WorkspaceScreenItem(
    StraumrWorkspace Workspace,
    StraumrWorkspaceEntry Entry)
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
