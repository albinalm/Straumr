using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.KeyValue;

internal sealed class EditFormField : TextField
{
    private bool _editing;
    private Scheme? _displayScheme;
    private Scheme? _editingScheme;
    private LineStyle _displayBorderStyle = LineStyle.Single;
    private LineStyle _editingBorderStyle = LineStyle.Double;

    public bool IsEditing => _editing;

    public Scheme? DisplayScheme
    {
        get => _displayScheme;
        set
        {
            _displayScheme = value;
            if (!_editing)
                ApplyScheme(_displayScheme);
        }
    }

    public Scheme? EditingScheme
    {
        get => _editingScheme;
        set
        {
            _editingScheme = value;
            if (_editing)
                ApplyScheme(_editingScheme);
        }
    }

    public LineStyle DisplayBorderStyle
    {
        get => _displayBorderStyle;
        set
        {
            _displayBorderStyle = value;
            if (!_editing)
                BorderStyle = value;
        }
    }

    public LineStyle EditingBorderStyle
    {
        get => _editingBorderStyle;
        set
        {
            _editingBorderStyle = value;
            if (_editing)
                BorderStyle = value;
        }
    }

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
        BorderStyle = _editingBorderStyle;
        ApplyScheme(_editingScheme);
        EditingStateChanged?.Invoke();
    }

    public void ExitEditMode()
    {
        _editing = false;
        BorderStyle = _displayBorderStyle;
        ApplyScheme(_displayScheme);
        EditingStateChanged?.Invoke();
    }

    private void ApplyScheme(Scheme? scheme)
    {
        if (scheme is null)
            return;

        SetScheme(scheme);
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
