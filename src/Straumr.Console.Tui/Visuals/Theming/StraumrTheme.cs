namespace Straumr.Console.Tui.Visuals.Theming;

/// <param name="Name">What the theme calls itself, for the footer to report.</param>
/// <param name="Warnings">
/// Roles the theme left indistinguishable from one another. Not errors — the theme still applies —
/// but each one is a cue the reader will not be able to see, which is worth saying once rather than
/// leaving them to wonder why a marker went missing.
/// </param>
public sealed record StraumrTheme(
    string Name,
    StraumrPalette Palette,
    IReadOnlyList<string> Warnings);
