using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Services.Interfaces;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Config;

public class ConfigWorkspacePathCommand(IStraumrOptionsService optionsService)
    : AsyncCommand<ConfigWorkspacePathCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(settings.Path))
        {
            string? currentPath = optionsService.Options.DefaultWorkspacePath;

            if (settings.Json)
            {
                System.Console.WriteLine(JsonSerializer.Serialize(
                    new ConfigWorkspacePathResult(currentPath),
                    CliJsonContext.Relaxed.ConfigWorkspacePathResult));
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $"[grey]Default workspace path:[/] {Markup.Escape(currentPath ?? "null")}");
            }

            return 0;
        }

        optionsService.Options.DefaultWorkspacePath = settings.Path;
        Directory.CreateDirectory(settings.Path);
        await optionsService.Save();

        if (settings.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(
                new ConfigWorkspacePathResult(settings.Path),
                CliJsonContext.Relaxed.ConfigWorkspacePathResult));
        }
        else
        {
            AnsiConsole.MarkupLine(
                $"[green]Updated default workspace path[/] [bold]{Markup.Escape(settings.Path)}[/]");
        }

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Set the default workspace path. Omit to display the current value.")]
        public string? Path { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the result as JSON")]
        public bool Json { get; set; }
    }
}
