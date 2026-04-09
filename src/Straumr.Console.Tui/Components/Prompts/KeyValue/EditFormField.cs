using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.Prompts.KeyValue;

internal sealed class EditFormField : TextField
{
    private bool _editing;

    private Color _idleBorderColor = Color.Gray;
    private Color _focusBorderColor = Color.White;
    private Color _editBorderColor = Color.BrightGreen;

    public bool IsEditing => _editing;

    public Color IdleBorderColor { get => _idleBorderColor; set { _idleBorderColor = value; Border?.SetNeedsDraw(); } }
    public Color FocusBorderColor { get => _focusBorderColor; set { _focusBorderColor = value; Border?.SetNeedsDraw(); } }
    public Color EditBorderColor { get => _editBorderColor; set { _editBorderColor = value; Border?.SetNeedsDraw(); } }

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
            return;

        Border.GettingAttributeForRole += (_, e) =>
        {
            Color fg = _editing ? _editBorderColor : HasFocus ? _focusBorderColor : _idleBorderColor;
            e.Result = new TuiAttribute(fg, Color.Black);
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
