using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Workspace;

public class WorkspaceCopyCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceCopyCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspace workspace =
            await GetWorkspaceAsync(workspaceService, settings.Identifier, cancellationToken: cancellation);
        StraumrWorkspaceEntry newEntry =
            await workspaceService.CopyAsync(workspace.Id, settings.NewName, settings.Output, cancellation);

        if (settings.Json)
        {
            WorkspaceCreateResult result = new WorkspaceCreateResult(newEntry.Id.ToString(), settings.NewName, newEntry.Path);
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceCreateResult));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Copied workspace[/] [bold]{Markup.Escape(settings.Identifier)}[/] to [bold]{Markup.Escape(settings.NewName)}[/]");
        }

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Identifier>")]
        [Description("Name or ID of the workspace to copy")]
        public required string Identifier { get; set; }

        [CommandArgument(1, "<NewName>")]
        [Description("Name for the new workspace")]
        public required string NewName { get; set; }

        [CommandOption("-o|--output <DIR>")]
        [Description("Directory where the workspace folder should be created")]
        public string? Output { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the result as JSON")]
        public bool Json { get; set; }
    }
}
