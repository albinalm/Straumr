using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.TextInput;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class TextInputPromptScreen : PromptScreen<string?>
{
    private readonly TextInputPrompt _prompt;

    public TextInputPromptScreen(
        string title,
        string? initialValue,
        bool allowEmpty,
        bool isSecret,
        Func<string, string?>? validate)
    {
        Add(new Banner());

        Add(new HintsBar { Text = "Enter Save  Esc Cancel" });

        _prompt = Add(new TextInputPrompt
        {
            Title = title,
            InitialValue = initialValue,
            AllowEmpty = allowEmpty,
            IsSecret = isSecret,
            Validate = validate,
        });

        _prompt.Submitted += value => Complete(value);
        _prompt.CancelRequested += Cancel;
    }
}
