using Straumr.Console.Shared.Console;
using Straumr.Console.Shared.Helpers;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Screens.Base;
using Straumr.Console.Tui.Screens.Prompts;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Console;

public sealed class TuiInteractiveConsole(IStraumrFileService fileService) : IInteractiveConsole
{
    private readonly StraumrTheme _theme = ThemeLoader.Load(fileService).Theme;

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
        using var app = new TuiApp(_theme);
        app.Run(screen);
    }
}
