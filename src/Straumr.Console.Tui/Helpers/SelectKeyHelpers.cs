using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Helpers;

internal static class SelectKeyHelpers
{
    public static Select<string> WithMovementKeys(this Select<string> select)
    {
        select.KeyDown((_, e) =>
            Move(e, select.Items.Count, select.SelectedIndex, index => select.SelectedIndex = index));
        return select;
    }

    public static void AttachTo(ListBox<string> list) =>
        list.KeyDown((_, e) =>
            Move(e, list.Items.Count, list.SelectedIndex, index => list.SelectedIndex = index));

    private static void Move(KeyEventArgs e, int count, int selected, Action<int> select)
    {
        if (count == 0)
        {
            return;
        }

        int current = Math.Clamp(selected, 0, count - 1);
        int? target =
            TuiKeybindHelpers.Matches("Select.Next", e) ? current + 1 :
            TuiKeybindHelpers.Matches("Select.Previous", e) ? current - 1 :
            TuiKeybindHelpers.Matches("Select.First", e) ? 0 :
            TuiKeybindHelpers.Matches("Select.Last", e) ? count - 1 :
            null;

        if (target is null)
        {
            return;
        }

        select(Math.Clamp(target.Value, 0, count - 1));
        e.Handled = true;
    }
}
