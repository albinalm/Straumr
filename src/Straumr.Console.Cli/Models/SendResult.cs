using System.Text.Json;

namespace Straumr.Console.Cli.Models;

public record SendResult(
    int? Status,
    string? Reason,
    string? Version,
    double DurationMs,
    Dictionary<string, string[]> Headers,
    JsonElement? Body);
