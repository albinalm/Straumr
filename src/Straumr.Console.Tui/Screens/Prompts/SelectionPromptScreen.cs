using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.Selection;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class SelectionPromptScreen : PromptScreen<string?>
{
    public SelectionPromptScreen(
        string title,
        IReadOnlyList<string> items,
        Func<string, string>? converter,
        StraumrTheme? theme = null,
        bool enableFilter = true,
        bool enableTypeahead = false)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        string hints = enableFilter
            ? "j/k Navigate  Enter Select  / Filter  Esc Back"
            : (enableTypeahead
                ? "j/k Navigate  Enter Select  Type to search  Esc Back"
                : "j/k Navigate  Enter Select  Esc Back");

        Add(new HintsBar { Text = hints });

        SelectionPrompt prompt = Add(new SelectionPrompt
        {
            Title = title,
            Items = items,
            DisplayConverter = converter,
            Theme = theme,
            EnableFilter = enableFilter,
            EnableTypeahead = enableTypeahead,
        });

        prompt.SelectionAccepted += Complete;
        prompt.CancelRequested += Cancel;
    }
}
