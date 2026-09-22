using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Models;

public sealed record MethodPaletteModel(
    Color Get,
    Color Post,
    Color Put,
    Color Patch,
    Color Delete,
    Color Other);
