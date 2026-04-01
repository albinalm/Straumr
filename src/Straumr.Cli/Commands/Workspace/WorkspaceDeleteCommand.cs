using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceDeleteCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceDeleteCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        await workspaceService.Delete(settings.Identifier);
        AnsiConsole.MarkupLine($"[red]Deleted workspace[/] [bold]{settings.Identifier}[/]");
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the workspace to delete")]
        public required string Identifier { get; set; }
    }
}