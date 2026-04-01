using Spectre.Console;

namespace Straumr.Cli.Console;

/// <summary>
/// Reusable prompt helpers that work with <see cref="EscapeCancellableConsole"/>
/// to provide go-back-able, vim-navigable prompts.
/// </summary>
public static class PromptHelpers
{
    /// <summary>
    /// Show any Spectre prompt with escape-to-go-back support.
    /// Returns <c>default</c> (null for reference types) when the user presses Escape.
    /// Automatically retries when Escape only exited search mode.
    /// </summary>
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
                // Escape exited search mode — re-show the same prompt clean
            }
            catch (OperationCanceledException)
            {
                return default;
            }
        }
    }

    /// <summary>
    /// Show a simple selection menu with escape-to-go-back support.
    /// Returns <c>null</c> when the user presses Escape.
    /// </summary>
    public static async Task<string?> PromptMenuAsync(
        EscapeCancellableConsole console,
        string title,
        IEnumerable<string> choices)
    {
        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title(title)
            .EnableSearch()
            .SearchPlaceholderText("/")
            .AddChoices(choices);

        return await PromptAsync(console, prompt);
    }

    /// <summary>
    /// Show a message, wait for any key, then clear the lines.
    /// Useful for validation errors or informational messages in a menu loop.
    /// </summary>
    public static void ShowTransientMessage(string markup)
    {
        int msgTop = System.Console.CursorTop;
        AnsiConsole.MarkupLine(markup);
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        System.Console.ReadKey(true);
        EscapeCancellableConsole.ClearLines(msgTop);
    }

    /// <summary>
    /// Show a two-column table (or an empty message), wait for any key, then clear the lines.
    /// </summary>
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
