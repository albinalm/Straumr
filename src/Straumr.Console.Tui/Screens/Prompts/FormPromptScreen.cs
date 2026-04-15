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
        IReadOnlyList<FormCustomCommand>? customCommands = null,
        StraumrTheme? theme = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        List<string> hintParts = ["Enter Edit/Next", "j/k Navigate"];
        if (customCommands is { Count: > 0 })
        {
            hintParts.AddRange(customCommands.Select(command => command.HintText));
        }

        hintParts.Add("Esc Cancel");
        string hints = string.Join("  ", hintParts);

        Add(new HintsBar { Text = hints });

        _prompt = Add(new FormPrompt
        {
            Title = title,
            Fields = fields,
            CustomCommands = customCommands ?? [],
            Theme = theme,
        });

        _prompt.Submitted += result => Complete(result);
        _prompt.CancelRequested += Cancel;
    }

    public override bool OnKeyDown(Key key)
    {
        if (_prompt.HandleFormKeyDown(key))
        {
            return true;
        }

        return base.OnKeyDown(key);
    }

    protected override bool ShouldCancelOnEscape(Key key) => !_prompt.AnyFieldEditing;
}
