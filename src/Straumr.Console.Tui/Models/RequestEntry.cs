using Straumr.Core.Models;

namespace Straumr.Console.Tui.Models;

public sealed class RequestEntry
{
    public required StraumrWorkspaceEntry StraumrEntry { get; init; }
    public required string Display { get; init; }
    public required string Identifier { get; init; }
    public required string Status { get; init; }
    public required bool IsDamaged { get; init; }
    public required int? RequestCount  { get; init; }
    public required int? SecretCount  { get; init; }
    public required int? AuthCount  { get; init; }
    public required DateTimeOffset? LastAccessed { get; init; }
    public required string? Name { get; set; }
}