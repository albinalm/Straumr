namespace Straumr.Console.Cli.Models;

public record DryRunResult(
    string Method,
    string Uri,
    string? Auth,
    Dictionary<string, string> Headers,
    Dictionary<string, string> Params,
    string BodyType,
    string? Body);
