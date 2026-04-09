using Straumr.Console.Tui.Components.TextFields.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Components.TextFields;

internal sealed class PromptTextField(Action textChanged, Func<bool> submit, Func<bool> requestCancel)
    : ThemedTextField
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
