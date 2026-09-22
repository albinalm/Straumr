using Straumr.Core.Models;

namespace Straumr.Console.Cli.Commands.Workspace;

internal class WorkspaceListEntryModel
{
    public bool IsCurrent { get; init; }
    public StraumrWorkspace? Workspace { get; init; }
    public required StraumrWorkspaceEntry Entry { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? LastAccessed { get; init; }
}
