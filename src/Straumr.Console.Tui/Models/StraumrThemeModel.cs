namespace Straumr.Console.Tui.Models;

public sealed record StraumrThemeModel(
    string Name,
    StraumrPaletteModel Palette,
    IReadOnlyList<string> Warnings);
