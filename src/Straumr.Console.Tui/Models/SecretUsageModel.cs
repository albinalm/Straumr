namespace Straumr.Console.Tui.Models;

internal sealed record SecretUsageModel(Guid WorkspaceId, Guid ResourceId, string Workspace, string Resource,
    string Kind, string Field);
