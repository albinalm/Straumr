using Straumr.Console.Tui.Screens.Components.Shared;

namespace Straumr.Console.Tui.Screens.Components.Workspace;

internal sealed class WorkspaceDeleteDialog
{
    private readonly ConfirmDialog _dialog;

    public WorkspaceDeleteDialog(string workspaceName, Action confirm)
    {
        _dialog = new ConfirmDialog(
            "Delete workspace",
            $"Delete {workspaceName}?",
            "The workspace and all files stored inside it will be permanently removed.",
            "Delete",
            true,
            confirm);
    }

    public void Show() => _dialog.Show();
}
