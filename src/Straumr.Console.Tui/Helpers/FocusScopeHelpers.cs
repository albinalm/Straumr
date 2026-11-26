using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Helpers;

internal static class FocusScopeHelpers
{
    public static bool Owns(this Visual visual) => visual.HasFocus || visual.HasFocusWithin;

    public static bool IsTyping(this Visual visual)
    {
        for (Visual? node = visual.App?.FocusedElement; node is not null; node = node.Parent)
        {
            if (node is TextEditorBase or PromptEditor)
            {
                return true;
            }
        }

        return false;
    }

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

    public static void FocusStep(this Visual scope, int step)
    {
        if (scope.App is not { FocusedElement: { } focused } app)
        {
            return;
        }

        List<Visual> stops = Stops(scope).ToList();
        if (stops.Count == 0)
        {
            return;
        }

        int index = stops.IndexOf(focused);
        app.Focus(index < 0
            ? stops[step > 0 ? 0 : stops.Count - 1]
            : stops[(index + step + stops.Count) % stops.Count]);
    }

    public static void FocusPane(this TerminalApp app, Visual scope, int step)
    {
        List<Visual> panes = Stops(scope).Where(visual => visual is not (TextEditorBase or PromptEditor)).ToList();
        if (panes.Count == 0)
        {
            return;
        }

        int index = panes.FindIndex(pane => pane.Owns());
        app.Focus(panes[index < 0 ? 0 : (index + step + panes.Count) % panes.Count]);
    }

    public static T FocusesOnClick<T>(this T pane) where T : Visual =>
        pane.PointerPressed((_, e) =>
        {
            if (e.Button == TerminalMouseButton.Left && !pane.Owns() && pane.FocusInto())
            {
                e.Handled = true;
            }
        });

    public static bool FocusInto(this Visual pane)
    {
        if (pane.Owns())
        {
            return true;
        }

        if (pane.App is not { } app || Stops(pane).FirstOrDefault() is not { } target)
        {
            return false;
        }

        app.Focus(target);
        return true;
    }

    private static IEnumerable<Visual> Stops(Visual scope) =>
        scope.EnumerateVisualsDepthFirst()
            .Where(visual => visual.Focusable && visual.IsVisible && visual.IsEnabled && visual.IsTabStop && visual.IsReachable());
}
