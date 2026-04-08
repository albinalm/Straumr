using System;

namespace Straumr.Console.Shared.Integrations;

public sealed class ConsoleIntegrationCatalog
{
    private readonly List<Func<IConsoleIntegrationInstaller>> _installerFactories = [];

    public ConsoleIntegrationCatalog AddInstaller<TInstaller>()
        where TInstaller : IConsoleIntegrationInstaller, new()
    {
        _installerFactories.Add(static () => new TInstaller());
        return this;
    }

    public IReadOnlyList<IConsoleIntegration> Build()
    {
        var builder = new ConsoleIntegrationBuilder();

        foreach (Func<IConsoleIntegrationInstaller> factory in _installerFactories)
        {
            IConsoleIntegrationInstaller installer = factory();
            installer.Install(builder);
        }

        return builder.Build();
    }
}
