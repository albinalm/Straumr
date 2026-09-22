namespace Straumr.Console.Tui.Models;

public sealed record ResourceRowModel(
    string Name,
    string? Meta = null,
    bool HasContent = false,
    string? Detail = null,
    bool IsCurrent = false,
    bool IsBroken = false,
    ResourceTokenModel? LeadingToken = null);
