using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public class WorkspaceExportCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceExportCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WorkspaceExportCommandSettings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspace workspace =
            await GetWorkspaceAsync(workspaceService, settings.Workspace, cancellationToken: cancellation);
        string outputFile = await workspaceService.ExportAsync(workspace.Id, settings.OutputPath, cancellation);

        if (settings.Json)
        {
            var result = new WorkspaceExportResult(outputFile);
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceExportResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Exported workspace to:[/] {Markup.Escape(outputFile)}");
        }

        return 0;
    }
}
