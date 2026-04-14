using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Workspace;

public class WorkspaceImportCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceImportCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry entry = await workspaceService.ImportAsync(settings.Path);

        if (settings.Json)
        {
            StraumrWorkspace workspace = await workspaceService.PeekWorkspaceAsync(entry.Path);
            WorkspaceCreateResult result = new WorkspaceCreateResult(entry.Id.ToString(), workspace.Name, entry.Path);
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceCreateResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Imported workspace from[/] {Markup.Escape(settings.Path)}");
        }

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Path>")]
        [Description("Path to the workspace file to import")]
        public required string Path { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the imported workspace as JSON")]
        public bool Json { get; set; }
    }
}