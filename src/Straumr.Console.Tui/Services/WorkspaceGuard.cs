using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Services.Interfaces;
using Straumr.Core.Models;

namespace Straumr.Console.Tui.Services;

public sealed class WorkspaceGuard(
    ScreenNavigationContext navigationContext,
    TuiInteractiveConsole interactiveConsole) : IWorkspaceGuard
{
    public WorkspaceGuardResult EnsureActiveWorkspace()
    {
        StraumrWorkspaceEntry? entry = navigationContext.GetWorkspaceEntry();
        if (entry is null)
        {
            interactiveConsole.ShowMessage(
                "No workspace selected.",
                "You will now be navigated to the workspaces menu. Set an active workspace to continue.");
            return WorkspaceGuardResult.Missing;
        }

        return new WorkspaceGuardResult(true, entry);
    }
}
