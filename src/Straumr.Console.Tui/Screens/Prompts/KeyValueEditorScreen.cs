using Straumr.Console.Tui.Components;
using Straumr.Console.Tui.Components.Prompts.KeyValue;
using Straumr.Console.Tui.Theme;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class KeyValueEditorScreen : PromptScreen<bool>
{
    public KeyValueEditorScreen(string title, IDictionary<string, string> items, TuiTheme? theme = null)
    {
        Add(new Banner
        {
            Text = Branding.Figlet,
            X = Pos.AnchorEnd(Branding.FigletWidth + 1),
            Y = 0,
        });

        var hints = Add(new HintsBar { Text = KeyValueEditorComponent.BrowseHints });

        var editor = Add(new KeyValueEditorComponent
        {
            Title = title,
            Items = items,
            Theme = theme,
        });

        editor.HintsChanged += hints.UpdateText;
        editor.DoneRequested += () => Complete(true);
    }
}
