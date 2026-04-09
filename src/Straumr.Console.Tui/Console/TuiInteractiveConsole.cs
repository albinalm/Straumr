using System.Linq;
using Straumr.Console.Shared.Console;
using Straumr.Console.Tui.Screens.Base;
using Straumr.Console.Tui.Screens.Prompts;
using Straumr.Console.Tui.Theme;

namespace Straumr.Console.Tui.Console;

public sealed class TuiInteractiveConsole : IInteractiveConsole
{
    private readonly TuiTheme _theme;

    public TuiInteractiveConsole(TuiTheme? theme = null)
    {
        _theme = theme ?? new TuiTheme();
    }

    public Task<string?> SelectAsync(string title, IReadOnlyList<string> choices, Func<string, string>? displayConverter = null)
    {
        var screen = new SelectionPromptScreen(title, choices, displayConverter, _theme);
        string? result = RunPrompt(screen);
        return Task.FromResult(result);
    }

    public Task<string?> TextInputAsync(
        string title,
        string? initialValue = null,
        bool allowEmpty = false,
        Func<string, string?>? validate = null)
    {
        var screen = new TextInputPromptScreen(title, initialValue, allowEmpty, false, validate);
        string? result = RunPrompt(screen);
        return Task.FromResult(result);
    }

    public Task<string?> SecretInputAsync(string title)
    {
        var screen = new TextInputPromptScreen(title, null, false, true, null);
        string? result = RunPrompt(screen);
        return Task.FromResult(result);
    }

    public void ShowMessage(string message)
    {
        RunPrompt(new MessagePromptScreen("Message", message));
    }

    public void ShowTable(
        string col1,
        string col2,
        IEnumerable<(string Key, string Value)> rows,
        string emptyMessage)
    {
        var screen = new TablePromptScreen("Details", col1, col2, rows.ToList(), emptyMessage);
        RunPrompt(screen);
    }

    public bool TryEditKeyValuePairs(string title, IDictionary<string, string> items, Action? onSaved = null)
    {
        var screen = new KeyValueEditorScreen(title, items, _theme, onSaved);
        RunScreen(screen);
        return true;
    }

    private TResult? RunPrompt<TResult>(PromptScreen<TResult> screen)
    {
        RunScreen(screen);
        return screen.Result;
    }

    private void RunScreen(Screen screen)
    {
        var app = new TuiApp(_theme);
        app.Run(screen);
    }
}
