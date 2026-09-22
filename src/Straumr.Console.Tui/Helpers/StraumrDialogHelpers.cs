using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Helpers;

internal static class StraumrDialogHelpers
{
    public const string CancelCommandId = "StraumrDialog.Cancel";

    public static Dialog CreateScreen(Visual content)
    {
        var dialog = new Dialog(content)
        {
            Padding = new Thickness(0),
            IsModal = true,
            IsDraggable = false,
            IsResizable = false,
            Left = 0,
            Top = 0
        };
        dialog.SetStyle(StraumrStyleService.Dialog);
        dialog.Width(() => TerminalViewportHelpers.Columns(dialog));
        dialog.Height(() => TerminalViewportHelpers.Rows(dialog));
        return dialog;
    }

    public static Dialog Create(Visual title, Visual content, int width)
    {
        var dialog = new Dialog(title, content)
        {
            Width = width,
            Padding = new Thickness(2, 1, 2, 1),
            IsModal = true,
            IsDraggable = false,
            IsResizable = false
        };
        dialog.SetStyle(StraumrStyleService.Dialog);
        dialog.AddCommand(new Command
        {
            Id = CancelCommandId,
            LabelMarkup = "Cancel",
            Gesture = TuiKeybindHelpers.Get(CancelCommandId),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => dialog.Close()
        });
        return dialog;
    }
}
