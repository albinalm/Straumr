using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Config;

public class ConfigWorkspacePathCommand(IStraumrOptionsService optionsService)
    : AsyncCommand<ConfigWorkspacePathCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(settings.Path))
        {
            string currentPath = optionsService.Options.DefaultWorkspacePath ?? "null";
            AnsiConsole.MarkupLine($"[grey]Default workspace path:[/] {Markup.Escape(currentPath)}");
            return 0;
        }

        optionsService.Options.DefaultWorkspacePath = settings.Path;
        await optionsService.Save();

        AnsiConsole.MarkupLine(
            $"[green]Updated default workspace path[/] [bold]{Markup.Escape(settings.Path)}[/]");
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Set the default workspace path. Omit to display the current value.")]
        public string? Path { get; set; }
    }
}
