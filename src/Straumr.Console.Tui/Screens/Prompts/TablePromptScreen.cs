using Straumr.Console.Tui.Components.Prompts;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class TablePromptScreen : PromptScreen<bool>
{
    public TablePromptScreen(string title, string column1, string column2, IReadOnlyList<(string Key, string Value)> rows, string emptyMessage)
    {
        Add(new TablePrompt
        {
            Title = title,
            Column1 = column1,
            Column2 = column2,
            Rows = rows,
            EmptyMessage = emptyMessage,
        });
    }

    public override bool OnKeyDown(Key key)
    {
        Complete(true);
        return true;
    }
}
