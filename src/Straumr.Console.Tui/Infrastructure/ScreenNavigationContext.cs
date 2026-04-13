using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class ScreenNavigationContext(IStraumrOptionsService optionsService)
{
    private StraumrWorkspaceEntry? _pendingWorkspace;

    public void SetWorkspace(StraumrWorkspaceEntry workspace) => _pendingWorkspace = workspace;

    public StraumrWorkspaceEntry? GetWorkspaceEntry()
    {
        StraumrWorkspaceEntry? resolved = _pendingWorkspace ?? optionsService.Options.CurrentWorkspace;
        return resolved;
    }
}
