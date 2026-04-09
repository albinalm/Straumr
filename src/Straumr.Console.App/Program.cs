using Straumr.Console.Cli.Integration;
using Straumr.Console.Shared.Integrations;
using Straumr.Console.Tui.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace Straumr.Console.App;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ConsoleIntegrationCatalog catalog = new ConsoleIntegrationCatalog()
            .AddInstaller<CliConsoleIntegrationInstaller>()
            .AddInstaller<TuiConsoleIntegrationInstaller>();

        IReadOnlyList<IConsoleIntegration> integrations = catalog.Build();
        var services = new ServiceCollection();
        foreach (IConsoleIntegration consoleIntegration in integrations)
        {
            consoleIntegration.ConfigureServices(services);
        }

        await using ServiceProvider provider = services.BuildServiceProvider();
        (IConsoleIntegration integration, string[] integrationArgs) = ConsoleIntegrationResolver.Resolve(integrations, args);

        using var cts = new CancellationTokenSource();
        System.Console.CancelKeyPress += (_, eventArgs) =>
        {
            cts.Cancel();
            eventArgs.Cancel = true;
        };

        return await integration.RunAsync(provider, integrationArgs, cts.Token);
    }
}
