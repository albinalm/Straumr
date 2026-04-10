using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.KeyValue;
using Straumr.Console.Tui.Helpers;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class KeyValueEditorScreen : PromptScreen<bool>
{
    public KeyValueEditorScreen(string title, IDictionary<string, string> items, StraumrTheme? theme = null,
        Action? onSaved = null)
    {
        Add(new Banner
        {
            Theme = theme,
        });

        HintsBar hints = Add(new HintsBar { Text = KeyValueEditorComponent.BrowseHints });

        KeyValueEditorComponent editor = Add(new KeyValueEditorComponent
        {
            Title = title,
            Items = items,
            Theme = theme,
        });

        StatusNotificationBar statusNotificationBar = Add(new StatusNotificationBar());

        editor.HintsChanged += hints.UpdateText;
        editor.DoneRequested += () => Complete(true);
        editor.ItemSaved += () =>
        {
            onSaved?.Invoke();
            statusNotificationBar.ShowStatus($" {title} saved", ColorResolver.Resolve(theme?.Success ?? "BrightGreen"),
                ColorResolver.Resolve(theme?.Surface ?? "Black"));
        };
    }
}
