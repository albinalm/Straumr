using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Straumr.Console.Shared.Console;
using Straumr.Console.Shared.Helpers;
using Straumr.Console.Shared.Integrations;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Screens;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Integration;

public sealed class TuiConsoleIntegration : IConsoleIntegration
{
    public string Name => "tui";
    public IReadOnlyCollection<string> Aliases { get; } = ["ui"];
    public IReadOnlyCollection<string> Commands { get; } = [];
    public bool IsDefault => true;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IStraumrFileService, StraumrFileService>();
        services.AddSingleton<IStraumrOptionsService, StraumrOptionsService>();
        services.AddSingleton<IStraumrWorkspaceService, StraumrWorkspaceService>();
        services.AddSingleton<StraumrThemeOptions>(provider =>
        {
            var fileService = provider.GetRequiredService<IStraumrFileService>();
            return ThemeLoader.LoadAsync(fileService).GetAwaiter().GetResult();
        });
        services.AddSingleton<IInteractiveConsole, TuiInteractiveConsole>();
    }

    public async Task<int> RunAsync(IServiceProvider serviceProvider, string[] args, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (args.Contains("--version"))
        {
            Assembly assembly = typeof(TuiConsoleIntegration).Assembly;
            string version = assembly.GetName().Version?.ToString() ?? "unknown";
            System.Console.WriteLine(version);
            return 0;
        }

        var optionsService = serviceProvider.GetRequiredService<IStraumrOptionsService>();
        await optionsService.Load();

        var workspaceService = serviceProvider.GetRequiredService<IStraumrWorkspaceService>();
        var theme = serviceProvider.GetRequiredService<StraumrThemeOptions>();

        List<string> lines = LoadWorkspaceLines(optionsService, workspaceService);
        var screen = new WorkspaceScreen(lines);
        var app = new TuiApp(theme.Theme);
        app.Run(screen);

        return 0;
    }

    private static List<string> LoadWorkspaceLines(
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService)
    {
        if (optionsService.Options.Workspaces.Count == 0)
        {
            return ["No workspaces found."];
        }

        List<WorkspaceLine> items = optionsService.Options.Workspaces
            .Select(entry => BuildWorkspaceLine(entry, optionsService, workspaceService))
            .ToList();

        return items
            .OrderByDescending(item => item.LastAccessed)
            .Select(item => item.Display)
            .ToList();
    }

    private static WorkspaceLine BuildWorkspaceLine(
        StraumrWorkspaceEntry entry,
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService)
    {
        var name = "Unknown";
        string status;
        DateTimeOffset? lastAccessed = null;

        try
        {
            StraumrWorkspace workspace = workspaceService.PeekWorkspace(entry.Path).GetAwaiter().GetResult();
            name = workspace.Name;
            status = "Valid";
            lastAccessed = workspace.LastAccessed;
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            status = "Corrupt";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            status = "Missing";
        }

        bool isCurrent = optionsService.Options.CurrentWorkspace?.Id == entry.Id;
        string idShort = entry.Id.ToString("N")[..8];
        string marker = isCurrent ? "* " : "  ";
        var display = $"{marker}{name}  [{idShort}]  {status}";

        return new WorkspaceLine(display, lastAccessed);
    }

    private sealed record WorkspaceLine(string Display, DateTimeOffset? LastAccessed);
}

public sealed class TuiConsoleIntegrationInstaller : IConsoleIntegrationInstaller
{
    public void Install(IConsoleIntegrationBuilder builder)
    {
        builder.AddIntegration(new TuiConsoleIntegration());
    }
}
