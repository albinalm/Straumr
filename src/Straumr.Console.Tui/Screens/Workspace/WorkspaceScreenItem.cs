using Straumr.Core.Models;

namespace Straumr.Console.Tui.Screens.Workspace;

public sealed record WorkspaceScreenItem(
    StraumrWorkspace Workspace,
    StraumrWorkspaceEntry Entry,
    bool IsCurrent)
{
    public string Directory =>
        Path.GetDirectoryName(Entry.Path) ?? Entry.Path;
}
