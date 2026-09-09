using Straumr.Console.Shared.Integrations;
using Straumr.Console.Tui.Visuals;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Integration;

public sealed class TuiConsoleIntegration : IConsoleIntegration
{
    public string Name => "tui";
    public IReadOnlyCollection<string> Aliases { get; } = ["ui"];
    public IReadOnlyCollection<string> Commands { get; } = [];
    public bool IsDefault => true;
    public bool OnlyRunOnEntrypoint => true;

    public void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }

    public async Task<int> RunAsync(IServiceProvider serviceProvider, string[] args,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var mainVisual = new MainVisual(serviceProvider);

        await Terminal.RunAsync(
            mainVisual,
            async _ =>
            {
                await mainVisual.LoadRequestsAsync(cancellationToken);
                mainVisual.DismissCommandPromptIfUnfocused();
                return mainVisual.ExitRequested
                    ? TerminalLoopResult.Stop
                    : TerminalLoopResult.Continue;
            },
            new TerminalRunOptions(),
            cancellationToken);
        
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
