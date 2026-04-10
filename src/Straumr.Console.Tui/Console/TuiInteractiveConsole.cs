using Straumr.Console.Shared.Helpers;
using Straumr.Console.Shared.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Screens.Prompts;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Console;

public sealed class TuiInteractiveConsole(IStraumrFileService fileService, TuiAppResolver appResolver) : IInteractiveConsole
{
    private readonly StraumrTheme _theme = ThemeLoader.Load(fileService).Theme;

    public string? Select(string title, IReadOnlyList<string> choices, Func<string, string>? displayConverter = null)
    {
        var screen = new SelectionPromptScreen(title, choices, displayConverter, _theme);
        return RunPrompt(screen);
    }

    public string? TextInput(
        string title,
        string? initialValue = null,
        bool allowEmpty = false,
        Func<string, string?>? validate = null)
    {
        var screen = new TextInputPromptScreen(title, initialValue, allowEmpty, false, validate);
        return RunPrompt(screen);
    }

    public string? SecretInput(string title)
    {
        var screen = new TextInputPromptScreen(title, null, false, true, null);
        return RunPrompt(screen);
    }

    public void ShowMessage(string title, string message)
    {
        RunPrompt(new MessagePromptScreen(title, message, _theme));
    }

    public void ShowMessage(string message)
    {
        ShowMessage("Message", message);
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

    public void ShowDetails(
        string title,
        IEnumerable<(string Key, string Value)> rows,
        string emptyMessage = "No details available")
    {
        var screen = new ModelDetailsScreen(title, rows.ToList(), emptyMessage, _theme);
        RunPrompt(screen);
    }

    public bool TryEditKeyValuePairs(string title, IDictionary<string, string> items, Action? onSaved = null)
    {
        var screen = new KeyValueEditorScreen(title, items, _theme, onSaved);
        RunPrompt(screen);
        return true;
    }

    private TResult? RunPrompt<TResult>(PromptScreen<TResult> screen)
    {
        TuiApp app = appResolver.GetOrCreate(_theme, out bool ownsApp);
        try
        {
            return app.RunPrompt(screen);
        }
        finally
        {
            if (ownsApp)
            {
                app.Dispose();
                appResolver.Clear(app);
            }
        }
    }
}
