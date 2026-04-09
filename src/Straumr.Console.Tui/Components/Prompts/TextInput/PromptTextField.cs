using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.TextInput;

internal sealed class PromptTextField(Action textChanged, Func<bool> submit, Func<bool> requestCancel)
    : TextField
{
    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.Enter)
        {
            return submit();
        }

        if (key == Key.Esc || key == Key.C.WithCtrl)
        {
            return requestCancel();
        }

        bool handled = base.OnKeyDown(key);
        textChanged();
        return handled;
    }
}
