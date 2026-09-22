namespace Straumr.Console.Cli.Infrastructure;

public sealed class StraumrCommandRegistry
{
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Commands => _names;

    internal void Add(string name) => _names.Add(name);
}
