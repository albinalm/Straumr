using Straumr.Console.Tui.Components.Bars;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.Message;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class MessagePromptScreen : PromptScreen<bool>
{
    public MessagePromptScreen(string title, string message)
    {
        Add(new Banner());

        Add(new HintsBar { Text = "Press any key to continue" });

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
