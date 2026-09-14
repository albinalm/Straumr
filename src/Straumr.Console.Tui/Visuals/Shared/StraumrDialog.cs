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

    /// <summary>
    /// A modal that fills the terminal and carries no frame title: a full-screen view names itself
    /// with the shell's own header bar instead, so the frame it sits in is the window frame every
    /// screen has rather than a dialog's. It takes no padding either, so its rules run to the frame.
    /// </summary>
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
        dialog.SetStyle(StraumrStyles.Dialog);
        dialog.Width(() => dialog.App?.Terminal.Size.Columns ?? 80);
        dialog.Height(() => dialog.App?.Terminal.Size.Rows ?? 24);
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
