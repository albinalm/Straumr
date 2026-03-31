using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Extensions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceDeleteCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceDeleteCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")] public required string Name { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        await workspaceService.Delete(settings.Name);
        AnsiConsole.MarkupLine($"[red]Deleted workspace[/] [bold]{settings.Name}[/] ({settings.Name.ToStraumrId()}.straumr)");
        return 0;
    }
}
