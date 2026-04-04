using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceCopyCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceCopyCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        await workspaceService.Copy(settings.Identifier, settings.NewName, settings.Output);
        AnsiConsole.MarkupLine($"[green]Copied workspace[/] [bold]{Markup.Escape(settings.Identifier)}[/] to [bold]{Markup.Escape(settings.NewName)}[/]");
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
    }
}
