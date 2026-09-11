namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// One row of a resource list, as the list renders it rather than as Core models it.
/// </summary>
/// <param name="Name">The resource name, on the first line.</param>
/// <param name="Meta">
/// An optional short summary of what the resource holds, on the second line. A list whose rows all
/// omit it is one line high, which is what a list of plain names such as a folder browser needs.
/// </param>
/// <param name="HasContent">
/// Whether <paramref name="Meta"/> describes something present. A populated row reads amber and an
/// empty one reads inert, so an empty resource is recognisable without reading the text.
/// </param>
/// <param name="Detail">
/// An optional third line, trimmed from the front so its tail stays readable. A list whose rows all
/// omit it is laid out two lines high instead of three.
/// </param>
/// <param name="IsCurrent">
/// Whether this is the resource the app currently acts on, such as the active workspace. The list
/// marks it with a dot so the screen answers "which one is live" without a trip to the header. A
/// list whose rows all omit it reserves no column for the marker.
/// </param>
/// <param name="IsBroken">
/// Whether the resource cannot be used as it stands, such as a workspace whose file no longer parses.
/// The list reads its name in red, so a resource needing attention is recognisable from the list alone
/// rather than only after selecting it.
/// </param>
public sealed record ResourceRow(
    string Name,
    string? Meta = null,
    bool HasContent = false,
    string? Detail = null,
    bool IsCurrent = false,
    bool IsBroken = false);
