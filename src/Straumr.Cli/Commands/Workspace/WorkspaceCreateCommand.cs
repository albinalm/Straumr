using System.ComponentModel;
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
        await workspaceService.Create(workspace, settings.Output);

        AnsiConsole.MarkupLine($"[green]Created workspace[/] [bold]{workspace.Name}[/] ({workspace.Id})");
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name>")]
        [Description("Name of the workspace to create")]
        public required string Name { get; set; }

        [CommandOption("-o|--output <DIR>")]
        [Description("Directory where the workspace folder should be created")]
        public string? Output { get; set; }
    }
}
