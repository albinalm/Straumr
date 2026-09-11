namespace Straumr.Console.Tui.Visuals.Shared;

internal sealed class FileBrowserDialog(
    string? requestedPath,
    string? defaultPath,
    Action<string> select,
    Func<string, bool> includeFile,
    string title = "Select file",
    string confirmLabel = "Select file")
    : BrowserDialog(requestedPath, defaultPath, select, title, confirmLabel, selectsFiles: true)
{
    protected override bool IncludeFile(string path) => includeFile(path);
}
