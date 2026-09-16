namespace Straumr.Console.Tui.Infrastructure;

public sealed record TuiCommand(
    string Name,
    Func<string, CancellationToken, Task<TuiCommandResult>> ExecuteAsync)
{
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public Func<IEnumerable<string>>? ArgumentValues { get; init; }

    public Func<string, int, TuiCommandCompletion>? CompleteArgument { get; init; }

    public bool RunsInPlaceFromOtherScreens { get; init; }

    public bool AllowPrefixMatch { get; init; } = true;

    public bool Matches(string name) =>
        Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        Aliases.Any(alias => alias.Equals(name, StringComparison.OrdinalIgnoreCase));
}

public static class TuiCommandArguments
{
    public static bool TryParseSingle(string argument, out string value, out string? error)
    {
        string text = argument.Trim();
        value = string.Empty;
        error = null;
        if (text.Length == 0)
            return true;

        if (text[0] != '"')
        {
            if (text.Any(char.IsWhiteSpace))
            {
                error = "names containing spaces must be wrapped in double quotes";
                return false;
            }

            value = text;
            return true;
        }

        var parsed = new System.Text.StringBuilder();
        for (int index = 1; index < text.Length; index++)
        {
            char current = text[index];
            if (current == '"')
            {
                if (text[(index + 1)..].Trim().Length > 0)
                {
                    error = "unexpected text after the closing double quote";
                    return false;
                }

                value = parsed.ToString();
                return true;
            }

            if (current == '\\' && index + 1 < text.Length && text[index + 1] is '\\' or '"')
                current = text[++index];
            parsed.Append(current);
        }

        error = "missing closing double quote";
        return false;
    }
}

public readonly record struct TuiCommandResult(string? Message, bool IsError)
{
    public static readonly TuiCommandResult None = new(null, false);

    /// <summary>
    /// What a command answers when it needs a workspace and there is none. Every screen that works
    /// inside a workspace says it in these words, so the reader is told the same thing and given
    /// the same way out wherever they typed from.
    /// </summary>
    public static readonly TuiCommandResult NoWorkspace =
        new("no active workspace; use :ws use <workspace> to choose one", true);

    public static TuiCommandResult Ok(string message) => new(message, false);

    public static TuiCommandResult Failed(string message) => new(message, true);
}
