using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Helpers;

internal static class SelectKeyHelpers
{
    public static Select<string> WithMovementKeys(this Select<string> select)
    {
        AddMovementHint(select, "Select.Previous", "Previous option", -1);
        AddMovementHint(select, "Select.Next", "Next option", 1);
        select.KeyDown((_, e) =>
            Move(e, select.Items.Count, select.SelectedIndex, index => select.SelectedIndex = index));
        return select;
    }
    private static void AddMovementHint(Select<string> select, string id, string label, int step) =>
        select.AddCommand(new Command
        {
            Id = id,
            LabelMarkup = label,
            Gesture = TuiKeybindHelpers.Get(id),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            RouteGesture = false,
            CanExecute = _ => select.Items.Count > 0,
            Execute = _ => select.SelectedIndex = Math.Clamp(select.SelectedIndex + step, 0, select.Items.Count - 1)
        });

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
