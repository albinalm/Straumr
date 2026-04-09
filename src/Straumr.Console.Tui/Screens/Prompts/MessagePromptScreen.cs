using Straumr.Console.Tui.Components;
using Straumr.Console.Tui.Components.Prompts;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class MessagePromptScreen : PromptScreen<bool>
{
    public MessagePromptScreen(string title, string message)
    {
        Add(new Banner
        {
            X = Pos.AnchorEnd(Branding.FigletWidth + 1),
            Y = 0,
        });

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
