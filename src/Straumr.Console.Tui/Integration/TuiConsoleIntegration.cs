using Straumr.Console.Shared.Integrations;
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
        await Terminal.RunAsync(new VStack(), () => TerminalLoopResult.Continue, cancellationToken);
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
