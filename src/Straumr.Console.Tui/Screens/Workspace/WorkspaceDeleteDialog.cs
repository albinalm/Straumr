using Straumr.Console.Tui.Visuals.Shared;

namespace Straumr.Console.Tui.Screens.Workspace;

internal sealed class WorkspaceDeleteDialog
{
    private readonly ConfirmDialog _dialog;

    public WorkspaceDeleteDialog(string workspaceName, Action confirm) =>
        _dialog = new ConfirmDialog(
            "Delete workspace",
            $"Delete {workspaceName}?",
            "The workspace and all files stored inside it will be permanently removed.",
            "Delete",
            destructive: true,
            confirm);

    public void Show() => _dialog.Show();
}
