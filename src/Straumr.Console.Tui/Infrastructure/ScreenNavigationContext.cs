using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class ScreenNavigationContext(IStraumrOptionsService optionsService)
{
    private StraumrWorkspaceEntry? _pendingWorkspace;
    private Guid? _pendingRequestId;

    public void SetWorkspace(StraumrWorkspaceEntry workspace) => _pendingWorkspace = workspace;
    public void SetRequest(Guid requestId) => _pendingRequestId = requestId;

    public Guid? ConsumeRequestId()
    {
        Guid? requestId = _pendingRequestId;
        _pendingRequestId = null;
        return requestId;
    }

    public StraumrWorkspaceEntry? GetWorkspaceEntry()
    {
        StraumrWorkspaceEntry? resolved = _pendingWorkspace ?? optionsService.Options.CurrentWorkspace;
        return resolved;
    }
}
