namespace Straumr.Console.Shared.Integrations;

public interface IConsoleIntegration
{
    string Name { get; }
    IReadOnlyCollection<string> Aliases { get; }
    bool IsDefault { get; }
    Task<int> RunAsync(string[] args, CancellationToken cancellationToken);
}
