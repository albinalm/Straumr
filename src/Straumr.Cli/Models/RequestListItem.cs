namespace Straumr.Cli.Models;

public record RequestListItem(
    string Id,
    string Name,
    string Method,
    string Uri,
    string Status,
    string? LastAccessed);
