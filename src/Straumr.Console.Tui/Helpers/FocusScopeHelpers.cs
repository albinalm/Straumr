using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Helpers;

internal static class FocusScopeHelpers
{
    public static bool Owns(this Visual visual) => visual.HasFocus || visual.HasFocusWithin;

    public static bool IsReachable(this Visual visual)
    {
        for (Visual? node = visual.Parent; node is not null; node = node.Parent)
        {
            if (!node.IsVisible)
            {
                return false;
            }
        }

        return true;
    }

    public static void FocusPrevious(this TerminalApp app) => app.FocusStep(-1);

    public static void FocusStep(this TerminalApp app, int step)
    {
        if (app.FocusedElement is not { } focused)
        {
            return;
        }

        Visual scope = focused;
        while (scope is not IModalVisual { IsModal: true } && scope.Parent is { } parent)
        {
            scope = parent;
        }

        List<Visual> stops = scope.EnumerateVisualsDepthFirst()
            .Where(visual => visual.Focusable && visual.IsVisible && visual.IsEnabled && visual.IsTabStop && visual.IsReachable())
            .ToList();

        int index = stops.IndexOf(focused);
        if (index < 0)
        {
            return;
        }

        app.Focus(stops[(index + step + stops.Count) % stops.Count]);
    }
}
