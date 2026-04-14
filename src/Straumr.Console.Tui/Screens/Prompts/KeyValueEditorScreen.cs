using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.KeyValue;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class KeyValueEditorScreen : PromptScreen<bool>
{
    private readonly KeyValueEditorComponent _editor;

    public KeyValueEditorScreen(string title, IDictionary<string, string> items, StraumrTheme? theme = null,
        Action? onSaved = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        HintsBar hints = Add(new HintsBar { Text = KeyValueEditorComponent.BrowseHints });

        _editor = Add(new KeyValueEditorComponent
        {
            Title = title,
            Items = items,
            Theme = theme,
        });

        StatusNotificationBar statusNotificationBar = Add(new StatusNotificationBar());

        _editor.HintsChanged += hints.UpdateText;
        _editor.DoneRequested += () => Complete(true);
        _editor.ItemSaved += () =>
        {
            onSaved?.Invoke();
            statusNotificationBar.ShowStatus($"{title} saved", ColorResolver.Resolve(theme?.Success ?? "BrightGreen"),
                ColorResolver.Resolve(theme?.Surface ?? "Black"));
        };
    }

    protected override bool ShouldCancelOnEscape(Key key) => !_editor.IsEditingItem;
}
