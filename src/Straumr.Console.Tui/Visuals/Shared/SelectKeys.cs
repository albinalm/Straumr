using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// The movement keys a dropdown answers to, on the control itself and on the list it opens.
/// </summary>
/// <remarks>
/// Every dropdown in this app is a <c>Select&lt;string&gt;</c> — a choice is offered by the label it
/// reads as and never by the value behind it — so one concrete pair of helpers covers all of them.
/// The framework gives a dropdown the arrows and nothing else, in both states. Both states need the
/// Vim keys, and for the same reason: the closed control is where a value is usually changed, and
/// the open list is where it is chosen from more than a handful.
/// The keys are handled from the key event rather than registered as gestures because gesture
/// routing matches a character case-insensitively while <c>KeyGesture</c> equality does not, so a
/// routed <c>g</c> would claim <c>G</c> with it and jumping to the last choice would land on the
/// first. It is the same reason <see cref="ResourceList"/> and <see cref="ScrollableContent"/>
/// handle theirs in <c>OnKeyDown</c>.
/// </remarks>
internal static class SelectKeys
{
    /// <summary>Gives the closed dropdown the keys, beside the arrows it already answers to.</summary>
    public static Select<string> WithMovementKeys(this Select<string> select)
    {
        select.KeyDown((_, e) =>
            Move(e, select.Items.Count, select.SelectedIndex, index => select.SelectedIndex = index));
        return select;
    }

    /// <summary>
    /// Gives the open list the same keys. <c>Select</c> builds that list itself and exposes it only
    /// to the style's popup factory, which is where this is called from.
    /// </summary>
    /// <remarks>
    /// Assigning the index is the whole of the movement: the list scrolls to whatever becomes
    /// selected, and its selection is bound back to the dropdown, so these keys change the value
    /// exactly as the arrows already do rather than moving a cursor the dropdown knows nothing about.
    /// </remarks>
    public static void AttachTo(ListBox<string> list) =>
        list.KeyDown((_, e) =>
            Move(e, list.Items.Count, list.SelectedIndex, index => list.SelectedIndex = index));

    private static void Move(KeyEventArgs e, int count, int selected, Action<int> select)
    {
        if (count == 0)
            return;

        int current = Math.Clamp(selected, 0, count - 1);
        int? target = e.Char switch
        {
            'j' => current + 1,
            'k' => current - 1,
            'g' => 0,
            'G' => count - 1,
            _ => null
        };

        if (target is null)
            return;

        select(Math.Clamp(target.Value, 0, count - 1));
        e.Handled = true;
    }
}
