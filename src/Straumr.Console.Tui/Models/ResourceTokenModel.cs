using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Models;

public sealed record ResourceTokenModel(string Text, TextBlockStyle Style, TextBlockStyle? SelectedStyle = null);
