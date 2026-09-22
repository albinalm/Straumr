namespace Straumr.Console.Tui.Models;

public sealed class TuiCommandSetModel
{
    private const char ArgumentSeparator = ' ';

    private readonly List<TuiCommandModel> _commands = [];

    public void Add(TuiCommandModel command) => _commands.Add(command);

    public void Clear() => _commands.Clear();

    public TuiCommandModel? ResolveCommand(string input)
    {
        string text = input.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        int separator = text.IndexOf(ArgumentSeparator);
        string name = separator < 0 ? text : text[..separator];
        return Resolve(name) is [TuiCommandModel single] ? single : null;
    }

    public async Task<TuiCommandResultModel> ExecuteAsync(string input, CancellationToken cancellationToken)
    {
        string text = input.Trim();
        if (text.Length == 0)
        {
            return TuiCommandResultModel.None;
        }

        int separator = text.IndexOf(ArgumentSeparator);
        string name = separator < 0 ? text : text[..separator];
        string argument = separator < 0 ? string.Empty : text[(separator + 1)..].Trim();

        return Resolve(name) switch
        {
            [] => TuiCommandResultModel.Failed($"unknown command: {name}"),
            [TuiCommandModel single] => await single.ExecuteAsync(argument, cancellationToken),
            var ambiguous => TuiCommandResultModel.Failed(
                $"{name} matches {Join(ambiguous.Select(command => command.Name))}")
        };
    }

    public TuiCommandCompletionModel Complete(string text, int caret)
    {
        int position = Math.Clamp(caret, 0, text.Length);
        int separator = text.IndexOf(ArgumentSeparator);

        if (separator < 0 || position <= separator)
        {
            return Match(_commands.Select(command => command.Name), text[..position], 0);
        }

        TuiCommandModel? command = Resolve(text[..separator]) is [TuiCommandModel single] ? single : null;
        if (command is null)
        {
            return TuiCommandCompletionModel.None;
        }

        int argumentStart = separator + 1;
        if (command.CompleteArgument is { } completeArgument)
        {
            string argument = text[argumentStart..];
            int argumentCaret = Math.Clamp(position - argumentStart, 0, argument.Length);
            TuiCommandCompletionModel completion = completeArgument(argument, argumentCaret);
            return completion with { ReplaceStart = completion.ReplaceStart + argumentStart };
        }

        if (command.ArgumentValues is null)
        {
            return TuiCommandCompletionModel.None;
        }

        int start = Math.Min(argumentStart, position);
        return Match(command.ArgumentValues(), text[start..position], start);
    }

    private List<TuiCommandModel> Resolve(string name)
    {
        List<TuiCommandModel> named = _commands.FindAll(command => command.Matches(name));
        return named.Count > 0
            ? named
            : _commands.FindAll(command =>
                command.AllowPrefixMatch &&
                command.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
    }

    public static TuiCommandCompletionModel Match(IEnumerable<string> values, string typed, int start)
    {
        string match = UnquotePartial(typed);
        string[] candidates = values
            .Where(value => value.StartsWith(match, StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(QuoteIfNeeded)
            .ToArray();

        return candidates.Length == 0
            ? TuiCommandCompletionModel.None
            : new TuiCommandCompletionModel(candidates, start, typed.Length);
    }

    private static string QuoteIfNeeded(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\""
            : value;

    private static string UnquotePartial(string value)
    {
        string text = value.StartsWith('"') ? value[1..] : value;
        if (text.EndsWith('"'))
        {
            text = text[..^1];
        }

        return text.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private static string Join(IEnumerable<string> names) => string.Join(", ", names);
}
