namespace Straumr.Console.Tui.Visuals.Shared;

internal sealed class FileBrowserDialog : BrowserDialog
{
    private readonly Func<string, bool> _includeFile;

    public FileBrowserDialog(
        string? requestedPath,
        string? defaultPath,
        Action<string> select,
        Func<string, bool> includeFile,
        string title = "Select file",
        string confirmLabel = "Select file")
        : base(requestedPath, defaultPath, select, title, confirmLabel, selectsFiles: true)
    {
        _includeFile = includeFile;
    }

    protected override bool IncludeFile(string path) => _includeFile(path);
}
