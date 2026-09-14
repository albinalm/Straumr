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
    /// Whether <paramref name="visual"/> is in a subtree the user can currently see.
    /// </summary>
    /// <remarks>
    /// Tab traversal tests the candidate's own <c>IsVisible</c> and not its ancestors', so a
    /// focusable inside a hidden subtree still takes a Tab: the inactive screen in the shell's
    /// <c>ZStack</c> kept its list in the rotation, giving a Tab that appeared to do nothing and
    /// left no region titled. Reading every ancestor's <c>IsVisible</c> registers each of them with
    /// the binding graph, so a computed <c>IsTabStop</c> over this re-evaluates when a screen is
    /// shown or hidden. It does not track re-parenting, which retained screens never do.
    /// </remarks>
    public static bool IsReachable(this Visual visual)
    {
        for (Visual? node = visual; node is not null; node = node.Parent)
            if (!node.IsVisible)
                return false;
        return true;
    }
}
