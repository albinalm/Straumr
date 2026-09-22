using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Helpers;

internal static class TerminalViewportHelpers
{
    public static int Columns(Visual visual) => visual.App?.Root.Bounds.Width ?? 80;

    public static int Rows(Visual visual) => visual.App?.Root.Bounds.Height ?? 24;
}
