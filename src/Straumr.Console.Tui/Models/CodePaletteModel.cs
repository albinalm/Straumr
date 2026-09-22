using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Models;

public sealed record CodePaletteModel(
    Color Key,
    Color String,
    Color Number,
    Color Boolean,
    Color Null,
    Color Punctuation);
