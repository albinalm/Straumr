using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public class WorkspaceImportCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceImportCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WorkspaceImportCommandSettings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry entry = await workspaceService.ImportAsync(settings.Path, cancellation);

        if (settings.Json)
        {
            StraumrWorkspace workspace = await workspaceService.GetAsync(
                entry.Id, cancellationToken: cancellation);
            var result = new WorkspaceCreateResult(entry.Id.ToString(), workspace.Name, entry.Path);
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceCreateResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Imported workspace from[/] {Markup.Escape(settings.Path)}");
        }

        return 0;
    }
}
