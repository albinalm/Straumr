namespace Straumr.Console.Tui.Models;

public sealed record QuickStartWorkspaceModel(
    Guid Id, string Name, int Requests, int Auths, string? Directory, bool IsCurrent);
