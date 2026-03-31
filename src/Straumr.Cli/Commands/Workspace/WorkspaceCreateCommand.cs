using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceCreateCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceCreateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")] public required string Name { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        var workspace = new StraumrWorkspace { Name = settings.Name };
        await workspaceService.CreateAndOpen(workspace);

        AnsiConsole.MarkupLine($"[green]Created workspace[/] [bold]{workspace.Name}[/] ({workspace.Id}.straumr)");
        return 0;
    }
}
