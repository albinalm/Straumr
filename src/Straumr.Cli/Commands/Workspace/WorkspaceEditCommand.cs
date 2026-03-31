using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceEditCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceEditCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")] public required string Identifier { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        string tempPath = await workspaceService.PrepareEdit(settings.Identifier);

        try
        {
            string? editor = Environment.GetEnvironmentVariable("EDITOR");
            if (string.IsNullOrWhiteSpace(editor))
            {
                throw new StraumrException("No default editor is configured.", StraumrError.EntryNotFound);
            }

            Process? process = Process.Start(new ProcessStartInfo(editor, tempPath)
            {
                UseShellExecute = false
            });

            if (process is null)
            {
                AnsiConsole.MarkupLine("[red]Failed to open file in default editor[/]");
                return 1;
            }

            await process.WaitForExitAsync(cancellation);

            if (process.ExitCode != 0)
            {
                AnsiConsole.MarkupLine("[red]Editor exited with an error. Changes discarded.[/]");
                return 1;
            }

            await workspaceService.ApplyEdit(settings.Identifier, tempPath);
            AnsiConsole.MarkupLine($"[green]Workspace [bold]{settings.Identifier}[/] updated[/]");
            return 0;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
