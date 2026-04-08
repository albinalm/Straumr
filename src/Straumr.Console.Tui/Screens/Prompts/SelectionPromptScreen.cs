using Straumr.Console.Tui.Components.Prompts.Selection;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class SelectionPromptScreen : PromptScreen<string?>
{
    public SelectionPromptScreen(string title, IReadOnlyList<string> items, Func<string, string>? converter)
    {
        var prompt = Add(new SelectionPrompt
        {
            Title = title,
            Items = items,
            DisplayConverter = converter,
        });

        prompt.SelectionAccepted += value => Complete(value);
        prompt.CancelRequested += Cancel;
    }
}
