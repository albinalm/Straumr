using Straumr.Console.Tui.Components.Prompts.TextInput.Base;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.Prompts.KeyValue;

internal sealed class EditFormField(Color background, Color foreground) : ThemedTextField(background, foreground)
{
    private bool _editing;

    public Color IdleBorderColor { get; set; } = Color.Gray;

    public Color FocusBorderColor { get; set; } = Color.BrightGreen;

    public Color EditBorderColor { get; set; } = Color.White;

    public event Action? EditRequested;
    public event Action? EditCompleted;
    public event Action? EditCancelled;
    public event Action? NavigateUp;
    public event Action? NavigateDown;
    public event Action? ExitRequested;
    public event Action? EditingStateChanged;

    public void EnterEditMode()
    {
        _editing = true;
        MoveEnd();
        Border?.SetNeedsDraw();
        EditingStateChanged?.Invoke();
    }

    public void ExitEditMode()
    {
        _editing = false;
        Border?.SetNeedsDraw();
        EditingStateChanged?.Invoke();
    }

    protected override void OnHasFocusChanged(bool previousHasFocus, View? currentFocused, View? newFocused)
    {
        base.OnHasFocusChanged(previousHasFocus, currentFocused, newFocused);
        Border?.SetNeedsDraw();
    }

    public void WireBorderColor()
    {
        if (Border is null)
        {
            return;
        }

        Border.GettingAttributeForRole += (_, e) =>
        {
            Color fg = _editing ? EditBorderColor : HasFocus ? FocusBorderColor : IdleBorderColor;
            e.Result = new Attribute(fg, Color.Black);
            e.Handled = true;
        };
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
