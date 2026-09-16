using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Straumr.Console.Shared.Helpers;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class ExternalEditor
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(EditorCommand);

    /// <summary>
    /// Opens the file where the editor would have opened it anyway. <c>AtStart</c> is line 1,
    /// column 1, which <c>HasPosition</c> reports as no position, so no editor-specific caret
    /// argument is passed — which is what a file the reader is returning to should do.
    /// </summary>
    private static readonly EditorDocument NoPosition = EditorDocument.AtStart(string.Empty);

    public Task<string> EditJsonAsync(string json, CancellationToken cancellationToken) =>
        EditAsync(EditorDocument.AtStart(json), ".jsonc", cancellationToken);

    /// <summary>
    /// Opens a file that already exists on disk, in place, and returns what the editor saved.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="EditAsync"/> this hands over the real path rather than a temporary copy.
    /// It is for a file the reader owns and knows where to find — their settings — so their editor's
    /// history, marks and per-path configuration apply to it, and so a file they already had open
    /// is the same file. Nothing is written back: the editor saved it, and a save that this then
    /// overwrote would be the edit being thrown away.
    /// </remarks>
    public async Task<string> EditFileAsync(string path, CancellationToken cancellationToken)
    {
        string editor = EditorCommand ??
                        throw new ExternalEditorException("no default editor is configured");

        using Process process = Start(editor, path, NoPosition);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new ExternalEditorException($"editor exited with code {process.ExitCode}");

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <summary>
    /// Opens <paramref name="document"/> in the configured editor and returns what it saved.
    /// </summary>
    /// <param name="extension">
    /// The extension of the file the editor is handed, leading dot included. It is the only thing
    /// that tells the editor what language it has been given, and being given the reader's own
    /// highlighting, indentation and bracket matching is the reason a body is written out here.
    /// </param>
    /// <remarks>
    /// Parsing the result belongs to the caller. Only the caller knows what the text is meant to be and
    /// what should happen to text the editor made invalid, and deciding that here could only mean
    /// throwing the edit away.
    /// </remarks>
    public async Task<string> EditAsync(
        EditorDocument document, string extension, CancellationToken cancellationToken)
    {
        string editor = EditorCommand ??
                        throw new ExternalEditorException("no default editor is configured");
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);

        try
        {
            await File.WriteAllTextAsync(path, document.Text, cancellationToken);

            using Process process = Start(editor, path, document);
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

    private static Process Start(string editor, string path, EditorDocument document)
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

        // The caret arguments go after whatever the reader configured and before the file, which is
        // where every syntax below expects them.
        (IReadOnlyList<string> arguments, string target) = Position(path, document, command[0]);
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.ArgumentList.Add(target);

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

    /// <summary>
    /// The arguments that ask an editor to open at a position, and the file argument to pass with
    /// them — some editors take the position on the path itself.
    /// </summary>
    /// <remarks>
    /// There is no universal way to do this. Nothing in the <c>EDITOR</c> convention covers it, so
    /// every editor that can be asked has invented its own flag and the rest cannot be asked at all.
    /// This is therefore a table of the syntaxes that are documented, and an editor that is not in
    /// it is opened with no extra argument: an argument it does not understand is either a second
    /// file it would create or an error it would exit on, and opening at the top of the file — where
    /// it would have opened anyway — is the only safe thing to do with an editor we do not know.
    /// The name is matched without its directory or extension, so a full path to <c>nano.exe</c> is
    /// the same as <c>nano</c>, and an editor invoked through a wrapper script simply falls through.
    /// </remarks>
    private static (IReadOnlyList<string> Arguments, string Target) Position(
        string path, EditorDocument document, string executable)
    {
        if (!document.HasPosition)
            return ([], path);

        (int line, int column) = (document.Line, document.Column);
        return Path.GetFileNameWithoutExtension(executable).ToLowerInvariant() switch
        {
            "nano" or "pico" => ([$"+{line},{column}"], path),
            // Vim's +N takes a line and leaves the caret on its first non-blank character, which on
            // an indented empty line is the end of it — the same place the column would have named.
            "vim" or "nvim" or "vi" or "view" or "gvim" or "mvim" or "joe" or "gedit" =>
                ([$"+{line}"], path),
            "emacs" or "emacsclient" or "micro" or "kak" => ([$"+{line}:{column}"], path),
            "kate" => (["-l", line.ToString(), "-c", column.ToString()], path),
            "notepad++" => ([$"-n{line}", $"-c{column}"], path),
            // These read the position off the path rather than from a flag of their own.
            "hx" or "helix" or "subl" or "sublime_text" => ([], $"{path}:{line}:{column}"),
            "code" or "code-insiders" or "codium" or "vscodium" =>
                (["--goto"], $"{path}:{line}:{column}"),
            _ => ([], path)
        };
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
