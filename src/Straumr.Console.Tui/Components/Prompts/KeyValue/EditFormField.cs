using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.KeyValue;

internal sealed class EditFormField : TextField
{
    private bool _editing;

    public bool IsEditing => _editing;

    public event Action? EditRequested;
    public event Action? EditCompleted;
    public event Action? EditCancelled;
    public event Action? NavigateUp;
    public event Action? NavigateDown;
    public event Action? ExitRequested;

    public void EnterEditMode()
    {
        _editing = true;
        ReadOnly = false;
    }

    public void ExitEditMode()
    {
        _editing = false;
        ReadOnly = true;
    }

    protected override bool OnKeyDown(Key key)
    {
        if (!_editing)
        {
            if (key == Key.Enter)
            {
                EditRequested?.Invoke();
                return true;
            }

            if (key == Key.CursorUp || key.AsRune.Value == 'k')
            {
                NavigateUp?.Invoke();
                return true;
            }

            if (key == Key.CursorDown || key.AsRune.Value == 'j')
            {
                NavigateDown?.Invoke();
                return true;
            }

            if (key == Key.Esc)
            {
                ExitRequested?.Invoke();
                return true;
            }

            return true;
        }

        if (key == Key.Enter)
        {
            EditCompleted?.Invoke();
            return true;
        }

        if (key == Key.Esc)
        {
            EditCancelled?.Invoke();
            return true;
        }

        return base.OnKeyDown(key);
    }
}
