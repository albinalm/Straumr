using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceLoadCommand(IStraumrWorkspaceService workspaceService, IStraumrScope scope) : AsyncCommand<WorkspaceLoadCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        public required string Name { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        await workspaceService.Load(settings.Name);

        AnsiConsole.MarkupLine($"[green][bold]{scope.Workspace!.Name}[/] is now your active workspace[/]");
        return 0;
    }
}
