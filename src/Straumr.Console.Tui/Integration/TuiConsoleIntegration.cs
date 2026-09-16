using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Straumr.Console.Shared.Integrations;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Screens.Auth;
using Straumr.Console.Tui.Screens.Workspace;
using Straumr.Console.Tui.Screens.Request;
using Straumr.Console.Tui.Screens.Secret;
using Straumr.Core.Extensions;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Integration;

public sealed class TuiConsoleIntegration : IConsoleIntegration
{
    public string Name => "tui";
    public IReadOnlyCollection<string> Aliases { get; } = ["ui"];
    public IReadOnlyCollection<string> Commands { get; } = [];
    public bool IsDefault => true;
    public bool OnlyRunOnEntrypoint => true;

    /// <remarks>
    /// The shell and its screens are scoped rather than singletons. A theme change rebuilds them —
    /// framework styles are values handed to a control at construction, so a palette cannot be
    /// swapped under a tree that already exists — and a scope is what lets the old shell and every
    /// screen under it be dropped together for a new set. Everything they depend on stays a
    /// singleton, so the workspace registry, the options and the applied theme outlive the rebuild.
    /// </remarks>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddStraumrCore();
        services.TryAddSingleton<ExternalEditor>();
        services.TryAddSingleton<ThemeSelection>();
        services.TryAddScoped<WorkspaceScreen>();
        services.TryAddScoped<RequestScreen>();
        services.TryAddScoped<AuthScreen>();
        services.TryAddScoped<SecretScreen>();
        services.TryAddScoped<StraumrTuiApp>();
    }

    public async Task<int> RunAsync(IServiceProvider serviceProvider, string[] args,
        CancellationToken cancellationToken)
    {
        IStraumrOptionsService optionsService = serviceProvider.GetRequiredService<IStraumrOptionsService>();
        try
        {
            await optionsService.LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (RequestScreen.IsRecoverable(exception))
        {
            // The initial screen needs options before construction. Its normal load repeats this
            // inside the shell, where the existing screen-level error state can explain a failure.
        }
        // Settings are read and the theme applied before the shell is constructed, because every
        // visual in it is handed its colours as it is built.
        IStraumrSettingsService settingsService = serviceProvider.GetRequiredService<IStraumrSettingsService>();
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
            // A settings file that cannot be read leaves the defaults standing, which is a themed
            // app rather than no app.
        }

        serviceProvider.GetRequiredService<ThemeSelection>().Apply();

        IServiceScope scope = serviceProvider.CreateScope();
        var app = scope.ServiceProvider.GetRequiredService<StraumrTuiApp>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TerminalInstance terminal = await Terminal.RunAsync(
                    app.Root,
                    async context =>
                    {
                        await app.UpdateAsync(context.App, cancellationToken);
                        return app.ExitRequested || app.HasPendingExternalAction
                            ? TerminalLoopResult.Stop
                            : TerminalLoopResult.Continue;
                    },
                    new TerminalRunOptions(),
                    cancellationToken);

                if (app.ExitRequested || !app.HasPendingExternalAction)
                    break;

                await terminal.StopInputAsync(cancellationToken);
                await app.RunPendingExternalActionAsync(cancellationToken);

                if (!app.RestartRequested)
                    continue;

                // The palette changed under this shell. Build a new one on the screen the reader
                // was on, and let the old scope take the whole retained tree with it.
                TuiScreen resumeOn = app.CurrentScreen;
                TuiCommandResult said = app.Message;
                scope.Dispose();
                scope = serviceProvider.CreateScope();
                app = scope.ServiceProvider.GetRequiredService<StraumrTuiApp>();
                app.ResumeOn(resumeOn, said);
            }
        }
        catch (OperationCanceledException) when (app.ExitRequested)
        {
            // Ctrl+C cancels StraumrTuiApp's own interrupt source to unwind an in-flight load or
            // operation immediately rather than waiting for it to finish on its own, which surfaces
            // here as a cancellation of whatever Core call was in flight when it was pressed.
        }
        finally
        {
            scope.Dispose();
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
