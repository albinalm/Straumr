using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components;
using Straumr.Console.Tui.Components.Prompts.KeyValue;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class KeyValueEditorScreen : PromptScreen<bool>
{
    public KeyValueEditorScreen(string title, IDictionary<string, string> items, StraumrTheme? theme = null, Action? onSaved = null)
    {
        Add(new Banner
        {
            X = Pos.AnchorEnd(Branding.FigletWidth + 1),
            Y = 0,
            Theme = theme,
        });

        var hints = Add(new HintsBar { Text = KeyValueEditorComponent.BrowseHints });

        var editor = Add(new KeyValueEditorComponent
        {
            Title = title,
            Items = items,
            Theme = theme,
        });

        var statusBar = Add(new StatusBar());

        editor.HintsChanged += hints.UpdateText;
        editor.DoneRequested += () => Complete(true);
        editor.ItemSaved += () =>
        {
            onSaved?.Invoke();
            statusBar.ShowSuccess($" {title} saved");
        };
    }
}
