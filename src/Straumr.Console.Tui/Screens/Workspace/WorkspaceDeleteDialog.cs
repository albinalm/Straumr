using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Workspace;

internal sealed class WorkspaceDeleteDialog
{
    private readonly Dialog _dialog;

    public WorkspaceDeleteDialog(string workspaceName, Action confirm)
    {
        var cancelButton = new Button("Cancel")
        {
            AutoFocus = true
        };
        cancelButton.SetStyle(StraumrStyles.Button);

        var deleteButton = new Button("Delete");
        deleteButton.SetStyle(StraumrStyles.DangerButton);

        var actions = new HStack(cancelButton, deleteButton)
            .Spacing(1)
            .HorizontalAlignment(Align.End);

        var content = new VStack(
                new TextBlock($"Delete {workspaceName}?")
                    .Style(StraumrStyles.BrightText)
                    .Wrap(true),
                new TextBlock("The workspace and all files stored inside it will be permanently removed.")
                    .Style(StraumrStyles.MutedText)
                    .Wrap(true),
                actions)
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        _dialog = new Dialog(
            new TextBlock("Delete workspace").Style(StraumrStyles.RedText),
            content)
        {
            Width = 58,
            Padding = new Thickness(2, 1, 2, 1),
            IsModal = true,
            IsDraggable = false,
            IsResizable = false
        };
        _dialog.SetStyle(StraumrStyles.Dialog);
        _dialog.AddCommand(new Command
        {
            Id = "WorkspaceDeleteDialog.Cancel",
            LabelMarkup = "Cancel",
            Gesture = new KeyGesture(TerminalKey.Escape),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => _dialog.Close()
        });

        cancelButton.Click(() => _dialog.Close());
        deleteButton.Click(() =>
        {
            _dialog.Close();
            confirm();
        });
    }

    public void Show() => _dialog.Show();
}
