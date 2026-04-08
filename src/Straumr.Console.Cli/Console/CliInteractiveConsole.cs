using Spectre.Console;
using Straumr.Console.Shared.Console;

namespace Straumr.Console.Cli.Console;

public sealed class CliInteractiveConsole : IInteractiveConsole
{
    private readonly EscapeCancellableConsole _console = new(AnsiConsole.Console);

    public async Task<string?> SelectAsync(
        string title, IReadOnlyList<string> choices, Func<string, string>? displayConverter = null)
    {
        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title(title)
            .EnableSearch()
            .SearchPlaceholderText("/")
            .AddChoices(choices);

        if (displayConverter is not null)
        {
            prompt.UseConverter(displayConverter);
        }

        return await PromptWithRetryAsync(prompt);
    }

    public async Task<string?> TextInputAsync(
        string title, string? initialValue = null, bool allowEmpty = false,
        Func<string, string?>? validate = null)
    {
        var prompt = new TextPrompt<string>(title);

        if (allowEmpty)
        {
            prompt.AllowEmpty();
        }

        if (validate is not null)
        {
            prompt.Validate(value =>
            {
                string? error = validate(value);
                return error is null ? ValidationResult.Success() : ValidationResult.Error(error);
            });
        }

        _console.PrefillInput(initialValue);
        try
        {
            return await PromptWithRetryAsync(prompt);
        }
        finally
        {
            _console.PrefillInput(null);
        }
    }

    public async Task<string?> SecretInputAsync(string title)
    {
        TextPrompt<string> prompt = new TextPrompt<string>(title).Secret();
        return await PromptWithRetryAsync(prompt);
    }

    public void ShowMessage(string message)
    {
        int msgTop = System.Console.CursorTop;
        AnsiConsole.MarkupLine(message);
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        System.Console.ReadKey(true);
        EscapeCancellableConsole.ClearLines(msgTop);
    }

    public void ShowTable(
        string col1, string col2, IEnumerable<(string Key, string Value)> rows, string emptyMessage)
    {
        int listTop = System.Console.CursorTop;
        List<(string Key, string Value)> items = rows.ToList();

        if (items.Count == 0)
        {
            AnsiConsole.MarkupLine(emptyMessage);
        }
        else
        {
            var table = new Table();
            table.AddColumn(col1);
            table.AddColumn(col2);
            foreach ((string key, string value) in items)
            {
                table.AddRow(Markup.Escape(key), Markup.Escape(value));
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        System.Console.ReadKey(true);
        EscapeCancellableConsole.ClearLines(listTop);
    }

    private async Task<T?> PromptWithRetryAsync<T>(IPrompt<T> prompt)
    {
        while (true)
        {
            try
            {
                return await _console.PromptAsync(prompt);
            }
            catch (OperationCanceledException) when (_console.WasSearchCancelled)
            {
                // Escape exited search mode — re-show the prompt
            }
            catch (OperationCanceledException)
            {
                return default;
            }
        }
    }
}
