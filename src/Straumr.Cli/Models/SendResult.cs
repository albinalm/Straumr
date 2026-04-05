namespace Straumr.Cli.Models;

public record SendResult(
    int? Status,
    string? Reason,
    string? Version,
    double DurationMs,
    Dictionary<string, string[]> Headers,
    string? Body);
