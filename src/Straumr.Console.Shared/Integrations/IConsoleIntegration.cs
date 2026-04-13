using Microsoft.Extensions.DependencyInjection;

namespace Straumr.Console.Shared.Integrations;

public interface IConsoleIntegration
{
    string Name { get; }
    IReadOnlyCollection<string> Aliases { get; }
    IReadOnlyCollection<string> Commands { get; }
    bool IsDefault { get; }
    void ConfigureServices(IServiceCollection services);
    Task<int> RunAsync(IServiceProvider serviceProvider, string[] args, CancellationToken cancellationToken);
}
