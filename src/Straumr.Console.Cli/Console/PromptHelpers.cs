using Spectre.Console;
using Straumr.Core.Configuration;

namespace Straumr.Console.Cli.Console;

public static class PromptHelpers
{
    public static async Task<T?> PromptAsync<T>(EscapeCancellableConsole console, IPrompt<T> prompt)
    {
        while (true)
        {
            try
            {
                return await console.PromptAsync(prompt);
            }
            catch (OperationCanceledException) when (console.WasSearchCancelled)
            {
            }
            catch (OperationCanceledException)
            {
                return default;
            }
        }
    }

    public static async Task<string?> PromptTextAsync(
        EscapeCancellableConsole console, TextPrompt<string> prompt, string? initialValue)
    {
        console.PrefillInput(initialValue);
        try
        {
            return await PromptAsync(console, prompt);
        }
        finally
        {
            console.PrefillInput(null);
        }
    }

    public static async Task<string?> PromptMenuAsync(
        EscapeCancellableConsole console,
        string title,
        IEnumerable<string> choices)
    {
        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title(title)
            .EnableSearch()
            .SearchPlaceholderText(StraumrKeybinds.Hint("Cli.Search"))
            .AddChoices(choices);

        return await PromptAsync(console, prompt);
    }

    public static void ShowTransientMessage(string markup)
    {
        int msgTop = System.Console.CursorTop;
        AnsiConsole.MarkupLine(markup);
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        System.Console.ReadKey(true);
        EscapeCancellableConsole.ClearLines(msgTop);
    }

    public static void ShowTransientTable(
        string col1, string col2,
        IEnumerable<(string Key, string Value)> rows,
        string emptyMarkup)
    {
        int listTop = System.Console.CursorTop;
        List<(string Key, string Value)> items = rows.ToList();

        if (items.Count == 0)
        {
            AnsiConsole.MarkupLine(emptyMarkup);
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
}
