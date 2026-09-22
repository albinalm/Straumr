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
public class WorkspaceCreateCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WorkspaceCreateCommandSettings settings,
        CancellationToken cancellation)
    {
        var workspace = new StraumrWorkspace { Name = settings.Name };
        await workspaceService.CreateAsync(workspace, settings.Output);

        string workspacePath = Path.GetDirectoryName(
            workspaceService.GetEntry(workspace.Id).Path) ?? settings.Output ?? string.Empty;

        if (settings.Json)
        {
            var result = new WorkspaceCreateResult(workspace.Id.ToString(), workspace.Name, workspacePath);
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceCreateResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Created workspace[/] [bold]{workspace.Name}[/] ({workspace.Id})");
        }

        return 0;
    }
}
