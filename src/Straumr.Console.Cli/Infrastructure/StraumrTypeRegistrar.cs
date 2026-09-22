using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Infrastructure;

public sealed class StraumrTypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    private IServiceProvider? _provider;

    public ITypeResolver Build() =>
        _provider is not null
            ? new StraumrTypeResolver(_provider)
            : new StraumrTypeResolver(services.BuildServiceProvider());

    [UnconditionalSuppressMessage("AOT", "IL2067",
        Justification = "Types are registered at startup with known implementations")]
    public void Register(Type service, Type implementation)
    {
        services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        services.AddSingleton(service, _ => factory());
    }

    public void UseServiceProvider(IServiceProvider provider) => _provider = provider;
}
