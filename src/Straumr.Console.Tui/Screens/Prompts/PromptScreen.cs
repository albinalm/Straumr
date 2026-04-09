using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Prompts;

internal abstract class PromptScreen<TResult> : Screen
{
    private bool _completed;

    public TResult? Result { get; private set; }

    protected void Complete(TResult? result)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        Result = result;
        Quit();
    }

    protected void Cancel() => Complete(default);

    public override bool OnKeyDown(Key key)
    {
        if (key == Key.Esc || key == Key.C.WithCtrl)
        {
            Cancel();
            return true;
        }

        return false;
    }
}
