using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Workspace;

public class WorkspaceActivateCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceActivateCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        Straumr.Core.Models.StraumrWorkspace workspace =
            await GetWorkspaceAsync(workspaceService, settings.Identifier, cancellationToken: cancellation);
        await workspaceService.ActivateAsync(workspace.Id, cancellation);

        AnsiConsole.MarkupLine($"[green][bold]{settings.Identifier}[/] is now your active workspace[/]");
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the workspace to activate")]
        public required string Identifier { get; set; }
    }
}
