namespace Straumr.Console.Tui.Infrastructure;

public sealed record TuiCommand(
    string Name,
    Func<string, CancellationToken, Task<TuiCommandResult>> ExecuteAsync)
{
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public Func<IEnumerable<string>>? ArgumentValues { get; init; }

    public bool Matches(string name) =>
        Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        Aliases.Any(alias => alias.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public readonly record struct TuiCommandResult(string? Message, bool IsError)
{
    public static readonly TuiCommandResult None = new(null, false);

    public static TuiCommandResult Ok(string message) => new(message, false);

    public static TuiCommandResult Failed(string message) => new(message, true);
}
