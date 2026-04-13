namespace Straumr.Console.Tui.Models;

public sealed class RequestEntry
{
    public required Guid Id { get; init; }
    public required string Display { get; init; }
    public required string Identifier { get; init; }
    public required string Status { get; init; }
    public required bool IsDamaged { get; init; }
    public required HttpMethod? Method { get; init; }
    public required string? ShortUriHostname { get; init; }
    public required string? BodyTypeText { get; init; }
    public required string? Auth { get; init; }
    public required DateTimeOffset? LastAccessed { get; init; }
    public required string? Name { get; init; }
}
