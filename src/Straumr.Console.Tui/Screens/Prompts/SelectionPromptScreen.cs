using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.Selection;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class SelectionPromptScreen : PromptScreen<string?>
{
    public SelectionPromptScreen(string title, IReadOnlyList<string> items, Func<string, string>? converter, StraumrTheme? theme = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        Add(new HintsBar { Text = "j/k Navigate  Enter Select  / Filter  Esc Back" });

        SelectionPrompt prompt = Add(new SelectionPrompt
        {
            Title = title,
            Items = items,
            DisplayConverter = converter,
            Theme = theme,
        });

        prompt.SelectionAccepted += Complete;
        prompt.CancelRequested += Cancel;
    }
}
