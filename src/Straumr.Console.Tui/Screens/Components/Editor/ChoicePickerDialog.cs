using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using ResourceList = Straumr.Console.Tui.Screens.Components.Shared.ResourceList;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class ChoicePickerDialog
{
    private const int DialogWidth = 48;
    private const int VisibleRows = 12;

    private readonly Dialog _dialog;

    public ChoicePickerDialog(string label, IReadOnlyList<string> labels, int selected, Action<int> choose)
    {
        var list = new ResourceList(labels.Select((text, index) => new ResourceRowModel(text, IsCurrent: index == selected)),
            activateLabel: "Choose")
        {
            AutoFocus = true,
            ActivatesOnClick = true,
            SelectsOnHover = true
        };
        list.SelectedIndex = Math.Clamp(selected, 0, Math.Max(0, labels.Count - 1));
        _dialog = StraumrDialogHelpers.Create(
            new TextBlock($"Choose {label}").Style(StraumrStyleService.AccentText),
            ResourceScreenLayoutHelpers.Scrollable(list).MinHeight(Math.Min(labels.Count, VisibleRows)).MaxHeight(VisibleRows),
            DialogWidth);
        list.ItemActivated += index =>
        {
            _dialog.Close();
            choose(index);
        };
        list.AddCommand(Cycle(list, "Straumr.NextField", "Next option", 1));
        list.AddCommand(Cycle(list, "Straumr.PreviousField", "Previous option", -1));
    }

    public void Show() => TuiWindowHelpers.Show(_dialog);

    private static Command Cycle(ResourceList list, string id, string label, int step) => new()
    {
        Id = id,
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get(id),
        Importance = CommandImportance.Secondary,
        Presentation = CommandPresentation.CommandBar,
        CanExecute = _ => list.Count > 0,
        Execute = _ => list.SelectedIndex = (list.SelectedIndex + step + list.Count) % list.Count
    };
}
