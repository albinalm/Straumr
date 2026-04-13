using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Straumr.Console.Shared.Helpers;
using Straumr.Console.Shared.Integrations;
using Straumr.Console.Shared.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Screens;
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
        services.AddSingleton(provider => provider.GetRequiredService<StraumrThemeOptions>().Theme);
        services.AddSingleton<TuiAppResolver>();
        services.AddSingleton<ScreenNavigationContext>();
        services.AddTransient<WorkspacesScreen>();
        services.AddTransient<RequestsScreen>();
        services.AddSingleton<TuiInteractiveConsole>();
        services.AddSingleton<IInteractiveConsole>(provider => provider.GetRequiredService<TuiInteractiveConsole>());
    }

    public async Task<int> RunAsync(IServiceProvider serviceProvider, string[] args,
        CancellationToken cancellationToken)
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

        var theme = serviceProvider.GetRequiredService<StraumrThemeOptions>();
        var resolver = serviceProvider.GetRequiredService<TuiAppResolver>();
        var engine = new ScreenEngine(serviceProvider, theme.Theme, resolver);
        if (optionsService.Options.CurrentWorkspace != null)
        {
            await engine.RunAsync<RequestsScreen>(cancellationToken);
        }
        else
        {
            await engine.RunAsync<WorkspacesScreen>(cancellationToken);
        }
        return 0;
    }
}

public sealed class TuiConsoleIntegrationInstaller : IConsoleIntegrationInstaller
{
    public void Install(IConsoleIntegrationBuilder builder)
    {
        builder.AddIntegration(new TuiConsoleIntegration());
    }
}