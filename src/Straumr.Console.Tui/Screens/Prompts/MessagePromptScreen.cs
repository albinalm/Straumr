using Straumr.Console.Tui.Components.Prompts;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class MessagePromptScreen : PromptScreen<bool>
{
    public MessagePromptScreen(string title, string message)
    {
        Add(new MessagePrompt
        {
            Title = title,
            Message = message,
        });
    }

    public override bool OnKeyDown(Key key)
    {
        Complete(true);
        return true;
    }
}
