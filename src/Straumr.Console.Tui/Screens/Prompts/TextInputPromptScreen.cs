using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.TextInput;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class TextInputPromptScreen : PromptScreen<string?>
{
    private readonly TextInputPrompt _prompt;

    public TextInputPromptScreen(
        string title,
        string? initialValue,
        bool allowEmpty,
        bool isSecret,
        Func<string, string?>? validate,
        StraumrTheme? theme = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        Add(new HintsBar { Text = "Enter Save  Esc Cancel" });

        _prompt = Add(new TextInputPrompt
        {
            Title = title,
            InitialValue = initialValue,
            AllowEmpty = allowEmpty,
            IsSecret = isSecret,
            Validate = validate,
            Theme = theme,
        });

        _prompt.Submitted += Complete;
        _prompt.CancelRequested += Cancel;
    }

    public override bool OnKeyDown(Key key)
    {
        if (_prompt.HandleInputKeyDown(key))
        {
            return true;
        }

        return base.OnKeyDown(key);
    }
}
