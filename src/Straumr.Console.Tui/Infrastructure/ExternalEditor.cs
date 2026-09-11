using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class ExternalEditor
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(EditorCommand);

    /// <summary>
    /// Opens <paramref name="json"/> in the configured editor and returns what it saved, as text.
    /// </summary>
    /// <remarks>
    /// Parsing the result belongs to the caller. Only the caller knows what the text is meant to be and
    /// what should happen to text the editor made invalid, and deciding that here could only mean
    /// throwing the edit away.
    /// </remarks>
    public async Task<string> EditJsonAsync(string json, CancellationToken cancellationToken)
    {
        string editor = EditorCommand ??
                        throw new ExternalEditorException("no default editor is configured");
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

        try
        {
            await File.WriteAllTextAsync(path, json, cancellationToken);

            using Process process = Start(editor, path);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new ExternalEditorException(
                    $"editor exited with code {process.ExitCode}; changes discarded");
            }

            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static string? EditorCommand =>
        Environment.GetEnvironmentVariable("EDITOR")?.Trim() is { Length: > 0 } editor
            ? editor
            : null;

    private static Process Start(string editor, string path)
    {
        IReadOnlyList<string> command = ParseCommand(editor);
        if (command.Count == 0)
            throw new ExternalEditorException("the configured editor command is empty");

        var startInfo = new ProcessStartInfo(command[0])
        {
            UseShellExecute = false
        };
        for (int index = 1; index < command.Count; index++)
            startInfo.ArgumentList.Add(command[index]);
        startInfo.ArgumentList.Add(path);

        try
        {
            return Process.Start(startInfo) ??
                   throw new ExternalEditorException("the configured editor could not be started");
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException)
        {
            throw new ExternalEditorException(
                $"the configured editor could not be started: {exception.Message}",
                exception);
        }
    }

    private static IReadOnlyList<string> ParseCommand(string command)
    {
        List<string> arguments = [];
        var current = new StringBuilder();
        char? quote = null;

        for (int index = 0; index < command.Length; index++)
        {
            char character = command[index];
            if (quote is null)
            {
                if (char.IsWhiteSpace(character))
                {
                    if (current.Length == 0)
                        continue;

                    arguments.Add(current.ToString());
                    current.Clear();
                }
                else if (character is '\'' or '"')
                {
                    quote = character;
                }
                else
                {
                    current.Append(character);
                }

                continue;
            }

            if (character == quote)
            {
                quote = null;
            }
            else if (character == '\\' &&
                     index + 1 < command.Length &&
                     command[index + 1] == quote)
            {
                current.Append(command[++index]);
            }
            else
            {
                current.Append(character);
            }
        }

        if (quote is not null)
            throw new ExternalEditorException("the configured editor command has an unmatched quote");

        if (current.Length > 0)
            arguments.Add(current.ToString());

        return arguments;
    }
}

public sealed class ExternalEditorException : Exception
{
    public ExternalEditorException(string message)
        : base(message)
    {
    }

    public ExternalEditorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
