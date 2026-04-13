using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Services.Interfaces;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Workspace;

public class WorkspaceExportCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceExportCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        string outputFile = await workspaceService.Export(settings.Workspace, settings.OutputPath);

        if (settings.Json)
        {
            WorkspaceExportResult result = new WorkspaceExportResult(outputFile);
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceExportResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Exported workspace to:[/] {Markup.Escape(outputFile)}");
        }

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the workspace to export")]
        public required string Workspace { get; set; }

        [CommandArgument(1, "<Output folder>")]
        [Description("Path to the folder where the exported file will be saved")]
        public required string OutputPath { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the result as JSON")]
        public bool Json { get; set; }
    }
}