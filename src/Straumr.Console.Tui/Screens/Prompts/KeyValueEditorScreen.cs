using Straumr.Console.Tui.Components.Prompts.KeyValue;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class KeyValueEditorScreen : PromptScreen<bool>
{
    public KeyValueEditorScreen(string title, IDictionary<string, string> items)
    {
        var editor = Add(new KeyValueEditorComponent
        {
            Title = title,
            Items = items,
        });

        editor.DoneRequested += () => Complete(true);
    }
}
