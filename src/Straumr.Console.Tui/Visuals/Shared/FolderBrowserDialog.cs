namespace Straumr.Console.Tui.Visuals.Shared;

internal sealed class FolderBrowserDialog : BrowserDialog
{
    public FolderBrowserDialog(
        string? requestedPath,
        string? defaultPath,
        Action<string> select,
        string title = "Select folder",
        string confirmLabel = "Select folder")
        : base(requestedPath, defaultPath, select, title, confirmLabel, selectsFiles: false)
    {
    }
}
