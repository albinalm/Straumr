using System.Diagnostics;
using Spectre.Console;
using static Straumr.Cli.Console.PromptHelpers;

namespace Straumr.Cli.Commands.Request;

internal static class RequestCommandHelpers
{
    internal static async Task<int?> LaunchEditorAsync(string editor, string path, CancellationToken cancellation)
    {
        Process? process = Process.Start(new ProcessStartInfo(editor, path)
        {
            UseShellExecute = false
        });

        if (process is null)
        {
            AnsiConsole.MarkupLine("[red]Editor exited with an error.[/]");
            return 1;
        }

        await process.WaitForExitAsync(cancellation);

        if (process.ExitCode != 0)
        {
            ShowTransientMessage("[red]Editor exited with an error. Changes discarded.[/]");
            return process.ExitCode;
        }

        return null;
    }
}