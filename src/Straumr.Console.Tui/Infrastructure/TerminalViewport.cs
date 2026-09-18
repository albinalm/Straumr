using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Infrastructure;

internal static class TerminalViewport
{
    public static int Columns(Visual visual) => visual.App?.Root.Bounds.Width ?? 80;

    public static int Rows(Visual visual) => visual.App?.Root.Bounds.Height ?? 24;
}
