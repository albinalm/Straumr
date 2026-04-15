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

public class WorkspaceCreateCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceCreateCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspace workspace = new StraumrWorkspace { Name = settings.Name };
        await workspaceService.CreateAsync(workspace, settings.Output);

        string workspacePath = Path.GetDirectoryName(
            workspaceService.GetWorkspaceEntryOnDisk(workspace.Id).Path) ?? settings.Output ?? string.Empty;

        if (settings.Json)
        {
            WorkspaceCreateResult result = new WorkspaceCreateResult(workspace.Id.ToString(), workspace.Name, workspacePath);
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceCreateResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Created workspace[/] [bold]{workspace.Name}[/] ({workspace.Id})");
        }

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

        [CommandOption("-j|--json")]
        [Description("Output the created workspace as JSON")]
        public bool Json { get; set; }
    }
}
