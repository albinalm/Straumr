namespace Straumr.Console.Tui.Models;

public sealed class SecretEntry
{
    public required Guid Id { get; init; }
    public required string Display { get; init; }
    public required string Identifier { get; init; }
    public required string Status { get; init; }
    public required bool IsDamaged { get; init; }
    public required DateTimeOffset? LastAccessed { get; init; }
    public required DateTimeOffset? Modified { get; init; }
    public required string? Name { get; init; }
}
