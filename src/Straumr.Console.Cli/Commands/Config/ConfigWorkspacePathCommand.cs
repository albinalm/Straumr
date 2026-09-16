using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Services.Interfaces;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Config;

/// <summary>
/// Reports where new workspaces are offered.
/// </summary>
/// <remarks>
/// It used to set the value too. The value now lives in <c>settings.toml</c>, which the program
/// reads and never writes: the file is hand-written and carries comments, and serialising a model
/// back over it would strip every one of them. So the command says where to put it rather than
/// putting it there, and a path passed to it is refused with that explanation instead of being
/// quietly ignored.
/// </remarks>
public class ConfigWorkspacePathCommand(IStraumrSettingsService settingsService)
    : AsyncCommand<ConfigWorkspacePathCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        await settingsService.LoadAsync(cancellation);

        if (!string.IsNullOrWhiteSpace(settings.Path))
            return Refuse(settings.Path);

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

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Display the default workspace path. It is set in settings.toml.")]
        public string? Path { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the result as JSON")]
        public bool Json { get; set; }
    }
}
