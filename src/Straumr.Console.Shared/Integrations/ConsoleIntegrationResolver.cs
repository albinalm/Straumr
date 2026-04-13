namespace Straumr.Console.Shared.Integrations;

public static class ConsoleIntegrationResolver
{
    public static (IConsoleIntegration DefaultIntegration, string[] RemainingArgs) Resolve(
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
            //Will return the default integration
            return (defaultIntegration, args);
        }

        string requestedName = args[0];
        IConsoleIntegration? requested = integrations.FirstOrDefault(integration =>
            NameMatches(integration.Name, requestedName) || HasMatchingAlias(integration, requestedName));

        if (requested is not null)
        {
            string[] remaining = args.Skip(1).ToArray();
            return (requested, remaining);
        }

        IConsoleIntegration? byCommand = integrations.FirstOrDefault(integration =>
            HasMatchingCommand(integration, requestedName));

        if (byCommand is not null)
        {
            //Will return the console integration that has a matching command
            return (byCommand, args);
        }

        return (defaultIntegration, args);
    }

    private static bool NameMatches(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private static bool HasMatchingAlias(IConsoleIntegration integration, string name) =>
        integration.Aliases.Any(alias => NameMatches(alias, name));

    private static bool HasMatchingCommand(IConsoleIntegration integration, string name) =>
        integration.Commands.Any(command => NameMatches(command, name));
}