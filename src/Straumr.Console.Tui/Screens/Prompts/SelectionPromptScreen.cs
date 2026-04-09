using Straumr.Console.Tui.Components;
using Straumr.Console.Tui.Components.Prompts.Selection;
using Straumr.Console.Tui.Theme;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class SelectionPromptScreen : PromptScreen<string?>
{
    public SelectionPromptScreen(string title, IReadOnlyList<string> items, Func<string, string>? converter, TuiTheme? theme = null)
    {
        Add(new Banner
        {
            Text = Branding.Figlet,
            X = Pos.AnchorEnd(Branding.FigletWidth + 1),
            Y = 0,
        });

        Add(new HintsBar { Text = "j/k Navigate  Enter Select  / Filter  Esc Back" });

        var prompt = Add(new SelectionPrompt
        {
            Title = title,
            Items = items,
            DisplayConverter = converter,
            Theme = theme,
        });

        prompt.SelectionAccepted += value => Complete(value);
        prompt.CancelRequested += Cancel;
    }
}
