using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Straumr.Core.Extensions;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui;

public sealed class TuiConsoleIntegration : IConsoleIntegration
{
    public string Name => "tui";
    public IReadOnlyCollection<string> Aliases { get; } = ["ui"];
    public IReadOnlyCollection<string> Commands { get; } = [];
    public bool IsDefault => true;
    public bool OnlyRunOnEntrypoint => true;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddStraumrCore();
        services.TryAddSingleton<ExternalEditorService>();
        services.TryAddSingleton<ThemeSelectionService>();
        services.TryAddSingleton<QuickStartService>();
        services.TryAddScoped<WorkspaceScreen>();
        services.TryAddScoped<RequestScreen>();
        services.TryAddScoped<AuthScreen>();
        services.TryAddScoped<SecretScreen>();
        services.TryAddScoped<StraumrTuiApp>();
    }

    public async Task<int> RunAsync(IServiceProvider serviceProvider, string[] args,
        CancellationToken cancellationToken)
    {
        var stateService = serviceProvider.GetRequiredService<IStraumrStateService>();
        try
        {
            await stateService.LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (RequestScreen.IsRecoverable(exception))
        {
        }
        var settingsService = serviceProvider.GetRequiredService<IStraumrSettingsService>();
        try
        {
            await settingsService.LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        var themeSelection = serviceProvider.GetRequiredService<ThemeSelectionService>();
        themeSelection.Apply();

        string? startupNotice = themeSelection.Message;

        if (!settingsService.Settings.QuickStartCompleted)
        {
            try
            {
                await serviceProvider.GetRequiredService<QuickStartService>().BeginAsync(cancellationToken);
            }
            catch (Exception exception) when (RequestScreen.IsRecoverable(exception))
            {
                startupNotice = $"quick start could not load: {exception.Message}";
            }
        }

        IServiceScope scope = serviceProvider.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<StraumrTuiApp>();
        if (startupNotice is not null)
        {
            app.Announce(TuiCommandResultModel.Failed(startupNotice));
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TerminalInstance terminal = await Terminal.RunAsync(
                    app.Root,
                    async context =>
                    {
                        await app.UpdateAsync(context.App, cancellationToken);
                        return app.ExitRequested || app.HasPendingExternalAction || app.RestartRequested
                            ? TerminalLoopResult.Stop
                            : TerminalLoopResult.Continue;
                    },
                    new TerminalRunOptions(),
                    cancellationToken);

                if (app.ExitRequested)
                {
                    break;
                }

                if (app.HasPendingExternalAction)
                {
                    await terminal.StopInputAsync(cancellationToken);
                    await app.RunPendingExternalActionAsync(cancellationToken);
                }

                if (!app.RestartRequested)
                {
                    continue;
                }

                TuiScreen resumeOn = app.CurrentScreen;
                TuiCommandResultModel said = app.Message;
                scope.Dispose();
                scope = serviceProvider.CreateScope();
                app = scope.ServiceProvider.GetRequiredService<StraumrTuiApp>();
                app.ResumeOn(resumeOn, said);
            }
        }
        catch (OperationCanceledException) when (app.ExitRequested)
        {
        }
        finally
        {
            scope.Dispose();
        }

        return 0;
    }
}
