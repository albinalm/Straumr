using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Extensions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceExportCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceExportCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<workspace name>")] public required string Workspace { get; set; }
        [CommandArgument(1, "<output path>")] public required string OutputPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        string outputFile = await workspaceService.Export(settings.Workspace, settings.OutputPath);
        AnsiConsole.MarkupLine(
            $"[green]Exported workspace to: [/] {outputFile}");
        return 0;
    }
}