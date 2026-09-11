namespace Straumr.Console.Tui.Visuals.Shared;

internal static class PathCompletion
{
    public static IReadOnlyList<string> Files(
        string text,
        string currentDirectory,
        Func<string, bool> includeFile)
    {
        try
        {
            string fullPath = Path.GetFullPath(
                text.Length == 0 ? "." : text,
                currentDirectory);
            if (Directory.Exists(fullPath) && !Path.EndsInDirectorySeparator(text))
                return [fullPath + Path.DirectorySeparatorChar];

            string parentPath;
            string prefix;
            if (Path.EndsInDirectorySeparator(text))
            {
                parentPath = fullPath;
                prefix = string.Empty;
            }
            else
            {
                parentPath = Path.GetDirectoryName(fullPath) ?? fullPath;
                prefix = Path.GetFileName(fullPath);
            }

            if (!Directory.Exists(parentPath))
                return [];

            IEnumerable<string> directories = Directory
                .EnumerateDirectories(parentPath)
                .Where(path => Path.GetFileName(path)
                    .StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(path => path + Path.DirectorySeparatorChar);
            IEnumerable<string> files = Directory
                .EnumerateFiles(parentPath)
                .Where(path => Path.GetFileName(path)
                    .StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(includeFile);

            return directories
                .Concat(files)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
                NotSupportedException or PathTooLongException)
        {
            return [];
        }
    }
}
