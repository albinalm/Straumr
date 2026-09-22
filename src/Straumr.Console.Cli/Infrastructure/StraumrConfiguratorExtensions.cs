using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Infrastructure;

public static class StraumrConfiguratorExtensions
{
    public static void AddStraumrBranch(this IConfigurator configurator,
        StraumrCommandRegistry registry,
        string name,
        Action<IConfigurator<CommandSettings>> action)
    {
        registry.Add(name);
        configurator.AddBranch(name, action);
    }

    public static void AddStraumrCommand<TCommand>(this IConfigurator configurator,
        StraumrCommandRegistry registry,
        string name)
        where TCommand : class, ICommand
    {
        registry.Add(name);
        configurator.AddCommand<TCommand>(name);
    }
}
