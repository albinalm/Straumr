using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceActivateCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceActivateCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        await workspaceService.Activate(settings.Identifier);

        AnsiConsole.MarkupLine($"[green][bold]{settings.Identifier}[/] is now your active workspace[/]");
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")] public required string Identifier { get; set; }
    }
}