namespace Straumr.Console.Cli.Models;

public record RequestGetResult(
    string Id,
    string Name,
    string Method,
    string Uri,
    string BodyType,
    Dictionary<string, string> Headers,
    Dictionary<string, string> Params,
    string? Body,
    string? AuthId,
    string LastAccessed,
    string Modified);
