using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceCreateCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceCreateCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        var workspace = new StraumrWorkspace { Name = settings.Name };
        await workspaceService.Create(workspace);

        AnsiConsole.MarkupLine($"[green]Created workspace[/] [bold]{workspace.Name}[/] ({workspace.Id})");
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name>")] public required string Name { get; set; }
    }
}