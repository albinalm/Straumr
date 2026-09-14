using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Straumr.Console.Shared.Integrations;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Screens.Workspace;
using Straumr.Console.Tui.Screens.Request;
using Straumr.Core.Extensions;
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

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddStraumrCore();
        services.TryAddSingleton<ExternalEditor>();
        services.TryAddSingleton<WorkspaceScreen>();
        services.TryAddSingleton<RequestScreen>();
        services.TryAddSingleton<StraumrTuiApp>();
    }

    public async Task<int> RunAsync(IServiceProvider serviceProvider, string[] args,
        CancellationToken cancellationToken)
    {
        var app = serviceProvider.GetRequiredService<StraumrTuiApp>();

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
            }
        }
        catch (OperationCanceledException) when (app.ExitRequested)
        {
            // Ctrl+C cancels StraumrTuiApp's own interrupt source to unwind an in-flight load or
            // operation immediately rather than waiting for it to finish on its own, which surfaces
            // here as a cancellation of whatever Core call was in flight when it was pressed.
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
