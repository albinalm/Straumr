using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.Form;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class FormPromptScreen : PromptScreen<Dictionary<string, string>?>
{
    public FormPromptScreen(
        string title,
        IReadOnlyList<FormFieldSpec> fields,
        StraumrTheme? theme = null,
        Func<FormFieldSpec, string?, string?>? browseHandler = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        Add(new HintsBar { Text = "Enter Edit/Next  j/k Navigate  Esc Cancel" });

        FormPrompt prompt = Add(new FormPrompt
        {
            Title = title,
            Fields = fields,
            Theme = theme,
            BrowseForPath = browseHandler,
        });

        prompt.Submitted += result => Complete(result);
        prompt.CancelRequested += Cancel;
    }
}
