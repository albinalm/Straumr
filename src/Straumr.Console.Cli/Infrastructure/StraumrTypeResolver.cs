using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Infrastructure;

public sealed class StraumrTypeResolver(IServiceProvider provider) : ITypeResolver
{
    public object? Resolve(Type? type) => type == null ? null : provider.GetService(type);
}
