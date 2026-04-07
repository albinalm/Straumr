using System.Diagnostics;
using Spectre.Console;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
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

    internal static async Task<StraumrWorkspaceEntry?> ResolveWorkspaceEntryAsync(
        string identifier,
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService)
    {
        if (Guid.TryParse(identifier, out Guid guid))
        {
            StraumrWorkspaceEntry? byId = optionsService.Options.Workspaces.FirstOrDefault(x => x.Id == guid);
            if (byId is not null)
            {
                return byId;
            }
        }

        foreach (StraumrWorkspaceEntry entry in optionsService.Options.Workspaces)
        {
            if (!File.Exists(entry.Path))
            {
                continue;
            }

            try
            {
                StraumrWorkspace ws = await workspaceService.PeekWorkspace(entry.Path);
                if (string.Equals(ws.Name, identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            catch
            {
                // ignored
            }
        }

        return null;
    }
}
