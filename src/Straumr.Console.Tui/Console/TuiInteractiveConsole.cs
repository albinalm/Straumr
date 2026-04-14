using Straumr.Console.Shared.Helpers;
using Straumr.Console.Shared.Interfaces;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Prompts.Form;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Screens.Prompts;
using Straumr.Core.Services.Interfaces;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Console;

public sealed class TuiInteractiveConsole(IStraumrFileService fileService, TuiAppResolver appResolver) : IInteractiveConsole
{
    private readonly StraumrTheme _theme = ThemeLoader.Load(fileService).Theme;

    public string? Select(
        string title,
        IReadOnlyList<string> choices,
        Func<string, string>? displayConverter = null,
        bool enableFilter = true,
        bool enableTypeahead = false)
    {
        var screen = new SelectionPromptScreen(title, choices, displayConverter, _theme, enableFilter, enableTypeahead);
        return RunPrompt(screen);
    }

    public string? TextInput(
        string title,
        string? initialValue = null,
        bool allowEmpty = false,
        Func<string, string?>? validate = null)
    {
        var screen = new TextInputPromptScreen(title, initialValue, allowEmpty, false, validate, _theme);
        return RunPrompt(screen);
    }

    public string? SecretInput(string title)
    {
        var screen = new TextInputPromptScreen(title, null, false, true, null, _theme);
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

    public Dictionary<string, string>? PromptForm(string title, IReadOnlyList<FormFieldSpec> fields)
    {
        var screen = new FormPromptScreen(title, fields, _theme, HandlePathBrowse);
        return RunPrompt(screen);

        string? HandlePathBrowse(FormFieldSpec spec, string? current)
        {
            return spec.PathMode switch
            {
                FormFieldPathMode.Directory => SelectDirectory($"Select {spec.Label}", current),
                FormFieldPathMode.ExistingFile => SelectFile($"Select {spec.Label}", current, spec.PathAllowedTypes),
                _ => current
            };
        }
    }

    public string? SelectDirectory(string title, string? initialPath = null)
    {
        var screen = new DirectorySelectPromptScreen(title, initialPath, _theme);
        return RunPrompt(screen);
    }

    public string? SelectFile(string title, string? initialPath = null, IReadOnlyList<IAllowedType>? allowedTypes = null)
        => SelectFile(title, initialPath, mustExist: true, allowedTypes);

    private string? SelectFile(string title, string? initialPath, bool mustExist, IReadOnlyList<IAllowedType>? allowedTypes)
    {
        IReadOnlyList<IAllowedType> types = allowedTypes is { Count: > 0 } ? allowedTypes : new[] { new AllowedTypeAny() };
        var screen = new FileSavePromptScreen(title, initialPath, types, _theme, mustExist);
        return RunPrompt(screen);
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
