namespace Straumr.Console.Tui.Models;

public sealed record TuiCommandModel(
    string Name,
    Func<string, CancellationToken, Task<TuiCommandResultModel>> ExecuteAsync)
{
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public Func<IEnumerable<string>>? ArgumentValues { get; init; }

    public Func<string, int, TuiCommandCompletionModel>? CompleteArgument { get; init; }

    public bool RunsInPlaceFromOtherScreens { get; init; }

    public bool AllowPrefixMatch { get; init; } = true;

    public bool Matches(string name) =>
        Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        Aliases.Any(alias => alias.Equals(name, StringComparison.OrdinalIgnoreCase));
}
