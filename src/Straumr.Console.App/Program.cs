using Straumr.Console.Cli.Integration;
using Straumr.Console.Cli.Commands.Autocomplete;
using Straumr.Console.Shared.Integrations;
#if INCLUDE_TUI
using Straumr.Console.Tui.Integration;
#endif
using Microsoft.Extensions.DependencyInjection;

namespace Straumr.Console.App;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        int? autocompleteExitCode = await AutocompleteRunner.TryRunAsync(args, cts.Token);
        if (autocompleteExitCode.HasValue)
        {
            return autocompleteExitCode.Value;
        }

        ConsoleIntegrationCatalog catalog = new ConsoleIntegrationCatalog()
            .AddInstaller<CliConsoleIntegrationInstaller>();

#if INCLUDE_TUI
        catalog.AddInstaller<TuiConsoleIntegrationInstaller>();
#endif

        IReadOnlyList<IConsoleIntegration> integrations = catalog.Build();
        var services = new ServiceCollection();
        foreach (IConsoleIntegration consoleIntegration in integrations)
        {
            consoleIntegration.ConfigureServices(services);
        }

        await using ServiceProvider provider = services.BuildServiceProvider();
        (IConsoleIntegration integration, string[] integrationArgs) = ConsoleIntegrationResolver.Resolve(integrations, args);

        return await integration.RunAsync(provider, integrationArgs, cts.Token);
    }
}
