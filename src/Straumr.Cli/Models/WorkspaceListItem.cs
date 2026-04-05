namespace Straumr.Cli.Models;

public record WorkspaceListItem(
    string Id,
    string Name,
    string Path,
    bool IsCurrent,
    int Requests,
    string Status,
    string? LastAccessed);
