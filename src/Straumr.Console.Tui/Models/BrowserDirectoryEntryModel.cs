namespace Straumr.Console.Tui.Models;

internal sealed record BrowserDirectoryEntryModel(
    string Name,
    string Path,
    bool IsParent,
    bool IsDrive,
    bool IsFile)
{
    public static BrowserDirectoryEntryModel Folder(string path) =>
        new(Label(path), path, false, false, false);

    public static BrowserDirectoryEntryModel File(string path) =>
        new(Label(path), path, false, false, true);

    public static BrowserDirectoryEntryModel Parent(string path) =>
        new(Label(path), path, true, false, false);

    public static BrowserDirectoryEntryModel Drive(string path) =>
        new(Label(path), path, false, true, false);

    private static string Label(string path)
    {
        string name = System.IO.Path.GetFileName(
            path.TrimEnd(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar));

        return name.Length > 0 ? name : path;
    }
}
