namespace Straumr.Console.Shared.Integrations;

public sealed class ConsoleIntegrationBuilder : IConsoleIntegrationBuilder
{
    private readonly List<IConsoleIntegration> _integrations = [];

    public void AddIntegration(IConsoleIntegration integration)
    {
        if (_integrations.Any(existing => NamesMatch(existing.Name, integration.Name)))
        {
            throw new InvalidOperationException($"An integration named '{integration.Name}' has already been registered.");
        }

        if (integration.Aliases.Any(alias => AliasConflicts(alias, integration.Name)))
        {
            throw new InvalidOperationException("Integration aliases must be unique.");
        }

        if (integration.IsDefault && _integrations.Any(existing => existing.IsDefault))
        {
            throw new InvalidOperationException("Only one integration can be marked as default.");
        }

        _integrations.Add(integration);
    }

    public IReadOnlyList<IConsoleIntegration> Build() => _integrations;

    private static bool NamesMatch(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private bool AliasConflicts(string alias, string integrationName)
    {
        if (NamesMatch(alias, integrationName))
        {
            return true;
        }

        foreach (IConsoleIntegration existing in _integrations)
        {
            if (NamesMatch(existing.Name, alias))
            {
                return true;
            }

            if (existing.Aliases.Any(existingAlias => NamesMatch(existingAlias, alias)))
            {
                return true;
            }
        }

        return false;
    }
}
