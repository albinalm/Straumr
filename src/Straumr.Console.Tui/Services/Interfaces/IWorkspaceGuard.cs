using Straumr.Core.Models;

namespace Straumr.Console.Tui.Services.Interfaces;

public interface IWorkspaceGuard
{
    WorkspaceGuardResult EnsureActiveWorkspace();
}

public sealed record WorkspaceGuardResult(bool HasWorkspace, StraumrWorkspaceEntry? WorkspaceEntry)
{
    public static WorkspaceGuardResult Missing { get; } = new(false, null);
}
