using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.TextInput;

internal sealed class PromptTextField : TextField
{
    private readonly Action _textChanged;
    private readonly Func<bool> _submit;
    private readonly Func<bool> _requestCancel;

    public PromptTextField(Action textChanged, Func<bool> submit, Func<bool> requestCancel)
    {
        _textChanged = textChanged;
        _submit = submit;
        _requestCancel = requestCancel;
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.Enter)
        {
            return _submit();
        }

        if (key == Key.Esc || key == Key.C.WithCtrl)
        {
            return _requestCancel();
        }

        bool handled = base.OnKeyDown(key);
        _textChanged();
        return handled;
    }
}
