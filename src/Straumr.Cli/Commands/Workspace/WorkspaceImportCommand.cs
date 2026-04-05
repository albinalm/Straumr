using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Services.Interfaces;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceImportCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceImportCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        await workspaceService.Import(settings.Path);
        AnsiConsole.MarkupLine($"[green]Imported workspace from[/] {settings.Path}");
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Path>")]
        [Description("Path to the workspace file to import")]
        public required string Path { get; set; }
    }
}