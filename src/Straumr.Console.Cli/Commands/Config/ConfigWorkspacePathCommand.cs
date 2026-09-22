using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Commands.Config;

[UsedImplicitly]
public class ConfigWorkspacePathCommand(IStraumrSettingsService settingsService)
    : AsyncCommand<ConfigWorkspacePathCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ConfigWorkspacePathCommandSettings settings,
        CancellationToken cancellation)
    {
        await settingsService.LoadAsync(cancellation);

        if (!string.IsNullOrWhiteSpace(settings.Path))
        {
            return Refuse(settings.Path);
        }

        string? currentPath = settingsService.DefaultWorkspacePath;
        if (settings.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(
                new ConfigWorkspacePathResult(currentPath),
                CliJsonContext.Relaxed.ConfigWorkspacePathResult));

            return 0;
        }

        AnsiConsole.MarkupLine(
            $"[grey]Default workspace path:[/] {Markup.Escape(currentPath ?? "null")}");
        AnsiConsole.MarkupLine($"[grey]Set it in[/] {Markup.Escape(settingsService.SettingsPath)}");
        return 0;
    }

    private int Refuse(string path)
    {
        AnsiConsole.MarkupLine(
            $"[yellow]The default workspace path is set in[/] " +
            $"{Markup.Escape(settingsService.SettingsPath)}[yellow], not here.[/]");
        AnsiConsole.MarkupLine(
            $"[grey]Add it under[/] [bold]{Markup.Escape("[paths]")}[/][grey]:[/] " +
            $"[bold]workspaces = \"{Markup.Escape(path)}\"[/]");
        return 1;
    }
}
