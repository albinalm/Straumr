using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public class WorkspaceActivateCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceActivateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WorkspaceActivateCommandSettings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspace workspace =
            await GetWorkspaceAsync(workspaceService, settings.Identifier, cancellationToken: cancellation);
        await workspaceService.ActivateAsync(workspace.Id, cancellation);

        AnsiConsole.MarkupLine($"[green][bold]{settings.Identifier}[/] is now your active workspace[/]");
        return 0;
    }
}
