namespace Straumr.Console.Tui.Models;

internal sealed record ReferenceUsageModel(Guid WorkspaceId, Guid ResourceId, string Workspace, string Resource,
    string Kind, string Field);
