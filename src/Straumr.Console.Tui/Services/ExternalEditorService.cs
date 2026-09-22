using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Straumr.Console.Tui.Services;

public sealed class ExternalEditorService
{

    private static readonly EditorDocumentModel NoPosition = EditorDocumentModel.AtStart(string.Empty);
    public bool IsConfigured => !string.IsNullOrWhiteSpace(EditorCommand);

    private static string? EditorCommand =>
        Environment.GetEnvironmentVariable("EDITOR")?.Trim() is { Length: > 0 } editor
            ? editor
            : null;

    public Task<string> EditJsonAsync(string json, CancellationToken cancellationToken) =>
        EditAsync(EditorDocumentModel.AtStart(json), ".jsonc", cancellationToken);

    public async Task<string> EditFileAsync(string path, CancellationToken cancellationToken)
    {
        string editor = EditorCommand ??
                        throw new ExternalEditorException("no default editor is configured");

        using Process process = Start(editor, path, NoPosition);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new ExternalEditorException($"editor exited with code {process.ExitCode}");
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task<string> EditAsync(
        EditorDocumentModel document, string extension, CancellationToken cancellationToken)
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
            {
                File.Delete(path);
            }
        }
    }

    private static Process Start(string editor, string path, EditorDocumentModel document)
    {
        IReadOnlyList<string> command = ParseCommand(editor);
        if (command.Count == 0)
        {
            throw new ExternalEditorException("the configured editor command is empty");
        }

        string executable = Resolve(command[0]);
        string name = Path.GetFileNameWithoutExtension(executable).ToLowerInvariant();

        List<string> arguments = [.. command.Skip(1)];
        foreach (string flag in Wait(name))
        {
            if (!arguments.Contains(flag, StringComparer.OrdinalIgnoreCase))
            {
                arguments.Add(flag);
            }
        }

        (IReadOnlyList<string> caret, string target) = Position(path, document, name);
        arguments.AddRange(caret);
        arguments.Add(target);

        ProcessStartInfo startInfo = Launch(executable, arguments);

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

    private static IReadOnlyList<string> Wait(string name) => name switch
    {
        "code" or "code-insiders" or "codium" or "vscodium" or "subl" or "sublime_text" or "gedit" =>
            ["--wait"],
        "gvim" or "mvim" => ["-f"],
        "kate" => ["-b"],
        _ => []
    };

    private static string Resolve(string command)
    {
        if (!OperatingSystem.IsWindows() ||
            command.AsSpan().IndexOfAny('/', '\\', ':') >= 0)
        {
            return command;
        }

        string[] extensions = Path.HasExtension(command)
            ? [string.Empty]
            : (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                 .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string extension in extensions)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(directory.Trim('"'), command + extension);
                }
                catch (ArgumentException)
                {
                    break;
                }

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return command;
    }

    private static ProcessStartInfo Launch(string executable, IReadOnlyList<string> arguments)
    {
        if (Path.GetExtension(executable).ToLowerInvariant() is not (".cmd" or ".bat") ||
            !OperatingSystem.IsWindows())
        {
            var direct = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (string argument in arguments)
            {
                direct.ArgumentList.Add(argument);
            }

            return direct;
        }

        var line = new StringBuilder("/d /s /c \"");
        line.Append(Quote(executable));
        foreach (string argument in arguments)
        {
            line.Append(' ').Append(Quote(argument));
        }

        line.Append('"');

        return new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
        {
            Arguments = line.ToString(),
            UseShellExecute = false
        };
    }

    private static string Quote(string value) =>
        value.Length > 0 && value.AsSpan().IndexOfAny(' ', '\t', '"') < 0
            ? value
            : $"\"{value.Replace("\"", "\\\"")}\"";

    private static (IReadOnlyList<string> Arguments, string Target) Position(
        string path, EditorDocumentModel document, string name)
    {
        if (!document.HasPosition)
        {
            return ([], path);
        }

        (int line, int column) = (document.Line, document.Column);
        return name switch
        {
            "nano" or "pico" => ([$"+{line},{column}"], path),
            "vim" or "nvim" or "vi" or "view" or "gvim" or "mvim" or "joe" or "gedit" =>
                ([$"+{line}"], path),
            "emacs" or "emacsclient" or "micro" or "kak" => ([$"+{line}:{column}"], path),
            "kate" => (["-l", line.ToString(), "-c", column.ToString()], path),
            "notepad++" => ([$"-n{line}", $"-c{column}"], path),
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
                    {
                        continue;
                    }

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
        {
            throw new ExternalEditorException("the configured editor command has an unmatched quote");
        }

        if (current.Length > 0)
        {
            arguments.Add(current.ToString());
        }

        return arguments;
    }
}
