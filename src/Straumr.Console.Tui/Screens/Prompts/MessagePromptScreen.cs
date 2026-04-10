using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Prompts.Message;
using Straumr.Console.Shared.Theme;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Prompts;

internal sealed class MessagePromptScreen : PromptScreen<bool>
{
    public MessagePromptScreen(string title, string message, StraumrTheme? theme = null)
    {
        Add(new Banner());

        Add(new MessagePrompt
        {
            Title = title,
            Message = message,
            Theme = theme,
        });
    }

    public override bool OnKeyDown(Key key)
    {
        Complete(true);
        return true;
    }
}
