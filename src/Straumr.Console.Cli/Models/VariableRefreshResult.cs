namespace Straumr.Console.Cli.Models;

public sealed record VariableRefreshResult(string Id, string Name, DateTimeOffset? CachedAt);
