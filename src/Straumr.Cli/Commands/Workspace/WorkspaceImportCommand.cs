using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceImportCommand(IStraumrWorkspaceService workspaceService) : AsyncCommand<WorkspaceImportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        public required string Path { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        await workspaceService.Import(settings.Path);
        AnsiConsole.MarkupLine($"[green]Imported workspace from[/] {settings.Path}");
        return 0;
    }
}
