using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Straumr.Cli.Utils;

public static class StraumrServices
{
    private static IServiceProvider? _serviceProvider;
    private static readonly ServiceCollection Services = [];

    public static T Get<T>() where T : notnull
    {
        return _serviceProvider == null
            ? throw new InvalidOperationException("Service provider has not been built")
            : _serviceProvider.GetRequiredService<T>();
    }

    public static void Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class
    {
        if (_serviceProvider != null)
        {
            throw new InvalidOperationException("Service provider has already been built");
        }

        Services.AddSingleton<T>();
    }
    
    public static void Add<TInterface, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface
    {
        if (_serviceProvider != null)
        {
            throw new InvalidOperationException("Service provider has already been built");
        }

        Services.AddSingleton<TInterface, TImplementation>();
    }
    
    public static void AddTransient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class
    {
        if (_serviceProvider != null)
        {
            throw new InvalidOperationException("Service provider has already been built");
        }

        Services.AddTransient<T>();
    }
    
    public static void AddTransient<TInterface, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface
    {
        if (_serviceProvider != null)
        {
            throw new InvalidOperationException("Service provider has already been built");
        }

        Services.AddTransient<TInterface, TImplementation>();
    }
    
    public static void Build()
    {
        if (_serviceProvider != null)
        {
            throw new InvalidOperationException("Service provider has already been built");
        }

        _serviceProvider = Services.BuildServiceProvider();
    }
}