using Straumr.Core.Models;

namespace Straumr.Console.Tui.Screens.Workspace;

public sealed record WorkspaceScreenItem(
    StraumrWorkspace Workspace,
    StraumrWorkspaceEntry Entry,
    bool IsCurrent)
{
    public string Directory =>
        Path.GetDirectoryName(Entry.Path) ?? Entry.Path;

    public string DisplayDirectory
    {
        get
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Directory.StartsWith(home, StringComparison.OrdinalIgnoreCase)
                ? $"~{Directory[home.Length..]}"
                : Directory;
        }
    }
}
