using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.Details;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class ModelDetailsScreen : PromptScreen<bool>
{
    public ModelDetailsScreen(
        string title,
        IReadOnlyList<(string Key, string Value)> rows,
        string emptyMessage = "No details available",
        StraumrTheme? theme = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        Add(new ModelDetailsPrompt
        {
            Title = title,
            Rows = rows,
            EmptyMessage = emptyMessage,
            Theme = theme,
        });
    }

    public override bool OnKeyDown(Key key)
    {
        if (base.OnKeyDown(key))
        {
            return true;
        }

        if (key == Key.Enter)
        {
            return true;
        }

        return false;
    }
}
