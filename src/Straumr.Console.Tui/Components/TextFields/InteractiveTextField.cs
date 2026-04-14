using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.TextFields;

/// <summary>
/// A flexible text field that supports configurable key bindings, theming, and edit-mode toggling.
/// </summary>
internal sealed class InteractiveTextField : TextField
{
    private readonly List<TextFieldKeyBinding> _bindings = [];
    private bool _useBorderColors;
    private Color _idleBorderColor;
    private Color _focusBorderColor;
    private Color _editingBorderColor;
    private Color _borderBackgroundColor = Color.Black;

    public bool IsEditing { get; private set; } = true;

    public event Action<bool>? EditingStateChanged;

    public void Bind(TextFieldKeyBinding binding) => _bindings.Add(binding);

    public void Bind(Key key, Func<InteractiveTextField, Key, bool> handler, bool clearText = false)
        => Bind(TextFieldKeyBinding.ForKey(key, handler, clearText));

    public void EnterEditMode()
    {
        if (IsEditing)
        {
            return;
        }

        IsEditing = true;
        MoveEnd();
        Border?.SetNeedsDraw();
        EditingStateChanged?.Invoke(true);
    }

    public void ExitEditMode()
    {
        if (!IsEditing)
        {
            return;
        }

        IsEditing = false;
        Border?.SetNeedsDraw();
        EditingStateChanged?.Invoke(false);
    }

    public void ApplyTheme(Color background, Color foreground)
    {
        var attr = new Attribute(foreground, background);
        Scheme scheme = new(attr)
        {
            Focus = new Attribute(attr),
            Editable = new Attribute(attr),
            HotActive = new Attribute(attr),
        };

        SetScheme(scheme);
    }

    public void SetBorderColors(Color idle, Color focus, Color editing)
        => SetBorderColors(idle, focus, editing, Color.Black);

    public void SetBorderColors(Color idle, Color focus, Color editing, Color background)
    {
        _idleBorderColor = idle;
        _focusBorderColor = focus;
        _editingBorderColor = editing;
        _borderBackgroundColor = background;
        _useBorderColors = true;
        HookBorderEvents();
    }

    private void HookBorderEvents()
    {
        if (Border is { } border)
        {
            border.GettingAttributeForRole -= OnBorderAttributeRequested;
            border.GettingAttributeForRole += OnBorderAttributeRequested;
        }
        else
        {
            Initialized -= OnInitializedForBorder;
            Initialized += OnInitializedForBorder;
        }
    }

    private void OnInitializedForBorder(object? sender, EventArgs e)
    {
        Initialized -= OnInitializedForBorder;
        HookBorderEvents();
    }

    private void OnBorderAttributeRequested(object? sender, VisualRoleEventArgs e)
    {
        if (!_useBorderColors)
        {
            return;
        }

        Color fg = IsEditing ? _editingBorderColor : HasFocus ? _focusBorderColor : _idleBorderColor;
        e.Result = new Attribute(fg, _borderBackgroundColor);
        e.Handled = true;
    }

    protected override bool OnKeyDown(Key key)
    {
        foreach (TextFieldKeyBinding binding in _bindings)
        {
            if (!binding.Matches(this, key))
            {
                continue;
            }

            if (binding.ClearText)
            {
                Text = string.Empty;
            }

            if (binding.Handle(this, key))
            {
                return true;
            }
        }

        if (!IsEditing)
        {
            return true;
        }

        return base.OnKeyDown(key);
    }

    protected override bool OnKeyDownNotHandled(Key key)
    {
        if (!IsEditing)
        {
            return true;
        }

        return base.OnKeyDownNotHandled(KeyHelpers.NormalizeForTextInput(key));
    }
}

internal sealed record TextFieldKeyBinding(
    Func<InteractiveTextField, Key, bool> Matches,
    Func<InteractiveTextField, Key, bool> Handle,
    bool ClearText = false)
{
    public static TextFieldKeyBinding ForKey(Key key, Func<InteractiveTextField, Key, bool> handler, bool clearText = false)
        => new((_, pressed) => pressed == key, handler, clearText);

    public static TextFieldKeyBinding When(Func<InteractiveTextField, Key, bool> predicate, Func<InteractiveTextField, Key, bool> handler, bool clearText = false)
        => new(predicate, handler, clearText);
}
