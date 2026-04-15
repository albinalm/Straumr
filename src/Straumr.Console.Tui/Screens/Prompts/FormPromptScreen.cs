using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.Form;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class FormPromptScreen : PromptScreen<Dictionary<string, string>?>
{
    private readonly FormPrompt _prompt;

    public FormPromptScreen(
        string title,
        IReadOnlyList<FormFieldSpec> fields,
        StraumrTheme? theme = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        Add(new HintsBar { Text = "Enter Edit/Next  j/k Navigate  Esc Cancel" });

        _prompt = Add(new FormPrompt
        {
            Title = title,
            Fields = fields,
            Theme = theme,
        });

        _prompt.Submitted += result => Complete(result);
        _prompt.CancelRequested += Cancel;
    }

    protected override bool ShouldCancelOnEscape(Key key) => !_prompt.AnyFieldEditing;
}
