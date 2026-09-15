using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// The two questions about focus that <c>Visual</c>'s own properties answer only for the visual
/// itself, and that every titled region and every focusable surface has to ask about a subtree.
/// </summary>
internal static class FocusScope
{
    /// <summary>
    /// Whether focus is on <paramref name="visual"/> or anywhere inside it.
    /// </summary>
    /// <remarks>
    /// <c>HasFocusWithin</c> excludes the visual itself, so a control that takes focus directly —
    /// a <c>TabControl</c>'s tab strip, for one — reports <see langword="false"/> for its own
    /// focus and leaves a title bound to it unlit while it is the focused element.
    /// </remarks>
    public static bool Owns(this Visual visual) => visual.HasFocus || visual.HasFocusWithin;

    /// <summary>
    /// Whether every ancestor of <paramref name="visual"/> is visible, so that a visual which is
    /// visible itself is one the user can actually see.
    /// </summary>
    /// <remarks>
    /// Tab traversal tests the candidate's own <c>IsVisible</c> and not its ancestors', so a
    /// focusable inside a hidden subtree still takes a Tab: the inactive screen in the shell's
    /// <c>ZStack</c> kept its list in the rotation, giving a Tab that appeared to do nothing and
    /// left no region titled. Reading every ancestor's <c>IsVisible</c> registers each of them with
    /// the binding graph, so a computed <c>IsTabStop</c> over this re-evaluates when a screen is
    /// shown or hidden. It does not track re-parenting, which retained screens never do.
    /// The walk starts at the parent. The visual's own visibility is the half traversal already
    /// tests, so reading it here would add nothing — and it is not free: one update pass may not
    /// both read and write the same bindable value, so a focusable that read its own
    /// <c>IsVisible</c> crashed the moment something computed that same property, which the pair
    /// dialog's value box did.
    /// </remarks>
    public static bool IsReachable(this Visual visual)
    {
        for (Visual? node = visual.Parent; node is not null; node = node.Parent)
            if (!node.IsVisible)
                return false;
        return true;
    }

    /// <summary>
    /// Moves focus to the previous tab stop, which is what the framework does for <c>Shift+Tab</c>
    /// and gives no way to ask for.
    /// </summary>
    /// <remarks>
    /// The window holding focus is the scope, which is what the framework uses too: the active
    /// modal when one is focused, and the screen otherwise. The order is the framework's own — every
    /// visual that is focusable, visible, enabled and a tab stop, in tree order — so stepping back
    /// through it is the exact inverse of <c>Tab</c> stepping forward. The framework's own walk
    /// takes children before their parent and this one takes the parent first; nothing in this app
    /// is a focusable inside a focusable, so the sequence is the same either way.
    /// </remarks>
    public static void FocusPrevious(this TerminalApp app)
    {
        if (app.FocusedElement is not { } focused)
            return;

        Visual scope = focused;
        while (scope.Parent is { } parent)
            scope = parent;

        List<Visual> stops = scope.EnumerateVisualsDepthFirst()
            .Where(visual => visual.Focusable && visual.IsVisible && visual.IsEnabled && visual.IsTabStop)
            .ToList();

        int index = stops.IndexOf(focused);
        if (index < 0)
            return;

        app.Focus(stops[(index - 1 + stops.Count) % stops.Count]);
    }
}
