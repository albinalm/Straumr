using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared;

internal static class StraumrDialog
{
    /// <summary>
    /// Named so a dialog whose content claims <c>Escape</c> for itself can take this one back out.
    /// </summary>
    public const string CancelCommandId = "StraumrDialog.Cancel";

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
        dialog.SetStyle(StraumrStyles.Dialog);
        dialog.AddCommand(new Command
        {
            Id = CancelCommandId,
            LabelMarkup = "Cancel",
            Gesture = new KeyGesture(TerminalKey.Escape),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => dialog.Close()
        });
        return dialog;
    }
}
