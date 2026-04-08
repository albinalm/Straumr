namespace Straumr.Console.Shared.Integrations;

public static class ConsoleIntegrationResolver
{
    public static (IConsoleIntegration Integration, string[] RemainingArgs) Resolve(
        IReadOnlyList<IConsoleIntegration> integrations,
        string[] args)
    {
        if (integrations.Count == 0)
        {
            throw new InvalidOperationException("No console integrations have been registered.");
        }

        IConsoleIntegration defaultIntegration =
            integrations.FirstOrDefault(integration => integration.IsDefault) ?? integrations[0];

        if (args.Length == 0)
        {
            return (defaultIntegration, args);
        }

        string requestedName = args[0];
        IConsoleIntegration? requested = integrations.FirstOrDefault(integration =>
            NameMatches(integration.Name, requestedName) || HasMatchingAlias(integration, requestedName));

        if (requested is null)
        {
            return (defaultIntegration, args);
        }

        string[] remaining = args.Skip(1).ToArray();
        return (requested, remaining);
    }

    private static bool NameMatches(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private static bool HasMatchingAlias(IConsoleIntegration integration, string name) =>
        integration.Aliases.Any(alias => NameMatches(alias, name));
}