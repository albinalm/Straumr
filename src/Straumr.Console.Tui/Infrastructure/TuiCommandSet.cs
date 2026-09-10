namespace Straumr.Console.Tui.Infrastructure;

public sealed class TuiCommandSet
{
    private const char ArgumentSeparator = ' ';

    private readonly List<TuiCommand> _commands = [];

    public void Add(TuiCommand command) => _commands.Add(command);

    public async Task<TuiCommandResult> ExecuteAsync(string input, CancellationToken cancellationToken)
    {
        string text = input.Trim();
        if (text.Length == 0)
            return TuiCommandResult.None;

        int separator = text.IndexOf(ArgumentSeparator);
        string name = separator < 0 ? text : text[..separator];
        string argument = separator < 0 ? string.Empty : text[(separator + 1)..].Trim();

        return Resolve(name) switch
        {
            [] => TuiCommandResult.Failed($"unknown command: {name}"),
            [TuiCommand single] => await single.ExecuteAsync(argument, cancellationToken),
            var ambiguous => TuiCommandResult.Failed(
                $"{name} matches {Join(ambiguous.Select(command => command.Name))}")
        };
    }

    public TuiCommandCompletion Complete(string text, int caret)
    {
        int position = Math.Clamp(caret, 0, text.Length);
        int separator = text.IndexOf(ArgumentSeparator);

        if (separator < 0 || position <= separator)
            return Match(_commands.Select(command => command.Name), text[..position], 0);

        TuiCommand? command = Resolve(text[..separator]) is [TuiCommand single] ? single : null;
        if (command?.ArgumentValues is null)
            return TuiCommandCompletion.None;

        int start = Math.Min(separator + 1, position);
        return Match(command.ArgumentValues(), text[start..position], start);
    }

    private List<TuiCommand> Resolve(string name)
    {
        List<TuiCommand> named = _commands.FindAll(command => command.Matches(name));
        return named.Count > 0
            ? named
            : _commands.FindAll(command =>
                command.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
    }

    private static TuiCommandCompletion Match(IEnumerable<string> values, string typed, int start)
    {
        string[] candidates = values
            .Where(value => value.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates.Length == 0
            ? TuiCommandCompletion.None
            : new TuiCommandCompletion(candidates, start, typed.Length);
    }

    private static string Join(IEnumerable<string> names) => string.Join(", ", names);
}

public readonly record struct TuiCommandCompletion(
    IReadOnlyList<string> Candidates,
    int ReplaceStart,
    int ReplaceLength)
{
    public static readonly TuiCommandCompletion None = new([], 0, 0);
}
