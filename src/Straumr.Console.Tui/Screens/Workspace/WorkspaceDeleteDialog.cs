using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

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

        _dialog = StraumrDialog.Create(
            new TextBlock("Delete workspace").Style(StraumrStyles.RedText),
            content,
            58);

        cancelButton.Click(() => _dialog.Close());
        deleteButton.Click(() =>
        {
            _dialog.Close();
            confirm();
        });
    }

    public void Show() => _dialog.Show();
}
