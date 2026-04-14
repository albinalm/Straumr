using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Text;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.Form;

internal sealed class FormFieldsView : View
{
    public required IReadOnlyList<FormFieldSpec> Fields { get; init; }
    public StraumrTheme? Theme { get; init; }

    public event Action<Dictionary<string, string>>? Submitted;
    public event Action? CancelRequested;

    private readonly List<InteractiveTextField> _fields = [];
    private readonly List<Button?> _sideButtons = [];
    private MarkupLabel? _inputErrorLabel;
    private Button? _saveButton;

    public FormFieldsView()
    {
        CanFocus = true;
    }

    public override void BeginInit()
    {
        base.BeginInit();

        int labelWidth = Fields.Count == 0 ? 0 : Fields.Max(f => f.Label.Length);
        int fieldX = 1 + labelWidth + 2;

        Color fieldBackground = Theme != null ? ColorResolver.Resolve(Theme.Surface) : Color.Black;
        Color fieldForeground = Theme != null ? ColorResolver.Resolve(Theme.OnSurface) : Color.White;

        int baseY = 1;
        for (int i = 0; i < Fields.Count; i++)
        {
            FormFieldSpec spec = Fields[i];
            int fieldY = baseY + (i * 3);
            int labelY = fieldY + 1;

            Label label = new Label
            {
                Text = spec.Label,
                X = 1,
                Y = labelY,
                Width = labelWidth,
            };

            FormFieldSideAction? sideAction = spec.SideAction;
            int sideButtonWidth = sideAction is null ? 0 : sideAction.Label.Length + 4;
            var field = new InteractiveTextField
            {
                X = fieldX,
                Y = fieldY,
                Width = sideAction is null ? Dim.Fill(2) : Dim.Fill(sideButtonWidth + 3),
                BorderStyle = LineStyle.Single,
            };

            ApplyFieldTheme(field, fieldBackground, fieldForeground);
            field.Text = spec.InitialValue ?? string.Empty;

            _fields.Add(field);
            Add(label, field);

            if (sideAction is not null)
            {
                Button sideButton = CreateButton(sideAction.Label);
                sideButton.X = Pos.AnchorEnd(sideButtonWidth);
                sideButton.Y = fieldY + 1;
                sideButton.Accepting += (_, _) => InvokeSideAction(field, sideAction);
                _sideButtons.Add(sideButton);
                Add(sideButton);
            }
            else
            {
                _sideButtons.Add(null);
            }
        }

        _saveButton = CreateButton("Save");
        _saveButton.X = fieldX;
        _saveButton.Y = baseY + (Fields.Count * 3) + 1;

        _inputErrorLabel = new MarkupLabel
        {
            X = 1,
            Y = _saveButton.Y + 2,
            Width = Dim.Fill(2),
            Height = 2,
            Visible = false,
            Theme = Theme,
        };

        Add(_saveButton, _inputErrorLabel);

        ConfigureNavigation();

        _saveButton.Accepting += (_, _) => TrySave();
        _saveButton.KeyDown += (_, key) =>
        {
            bool up = key == Key.CursorUp || KeyHelpers.GetCharValue(key) == 'k';
            bool down = key == Key.CursorDown || KeyHelpers.GetCharValue(key) == 'j';

            if (up)
            {
                key.Handled = true;
                FocusView(_fields.LastOrDefault());
            }
            else if (down)
            {
                key.Handled = true;
                FocusView(_fields.FirstOrDefault());
            }
            else if (key == Key.Esc)
            {
                key.Handled = true;
                CancelRequested?.Invoke();
            }
        };

        Initialized += (_, _) =>
        {
            FocusView(_fields.FirstOrDefault());
            _fields.FirstOrDefault()?.EnterEditMode();
        };

        KeyDown += (_, key) =>
        {
            if (key == Key.Esc && _fields.All(f => !f.IsEditing))
            {
                key.Handled = true;
                CancelRequested?.Invoke();
            }
        };
    }

    public void SetValues(IDictionary<string, string> values)
    {
        for (var i = 0; i < Fields.Count; i++)
        {
            string key = Fields[i].Key;
            if (values.TryGetValue(key, out string? value))
            {
                _fields[i].Text = value;
            }
            else if (Fields[i].InitialValue is not null)
            {
                _fields[i].Text = Fields[i].InitialValue!;
            }
            else
            {
                _fields[i].Text = string.Empty;
            }
        }
    }

    public void FocusFirstField()
    {
        FocusView(_fields.FirstOrDefault());
        _fields.FirstOrDefault()?.EnterEditMode();
    }

    private void ConfigureNavigation()
    {
        for (var i = 0; i < _fields.Count; i++)
        {
            int index = i;
            InteractiveTextField field = _fields[index];
            ConfigureEditField(field, Above, Below);

            Button? sideButton = index < _sideButtons.Count ? _sideButtons[index] : null;
            if (sideButton is not null)
            {
                sideButton.KeyDown += (_, key) =>
                {
                    if (key == Key.CursorUp || KeyHelpers.GetCharValue(key) == 'k')
                    {
                        key.Handled = true;
                        FocusView(_fields[index]);
                        _fields[index].EnterEditMode();
                    }
                    else if (key == Key.CursorDown || KeyHelpers.GetCharValue(key) == 'j')
                    {
                        key.Handled = true;
                        View target = index == _fields.Count - 1 ? _saveButton! : _fields[index + 1];
                        FocusView(target);
                        if (target is InteractiveTextField nextField)
                        {
                            nextField.EnterEditMode();
                        }
                    }
                    else if (key == Key.Esc)
                    {
                        key.Handled = true;
                        CancelRequested?.Invoke();
                    }
                };
            }

            View? Below() => index == _fields.Count - 1 ? _saveButton : _fields[index + 1];
            View? Above() => index == 0 ? _saveButton : _fields[index - 1];
        }
    }

    private Button CreateButton(string text)
    {
        Button button = new()
        {
            Text = text,
            ShadowStyle = ShadowStyle.None
        };
        button.Margin?.SetShadow(ShadowStyle.None);
        if (Theme is not null)
        {
            button.SetScheme(ColorResolver.BuildButtonScheme(Theme));
        }

        return button;
    }

    private void ConfigureEditField(InteractiveTextField field, Func<View?> above, Func<View?> below)
    {
        field.ExitEditMode();

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => !f.IsEditing && key == Key.Enter,
            (f, _) =>
            {
                f.EnterEditMode();
                return true;
            }));

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => !f.IsEditing && (key == Key.CursorUp || KeyHelpers.GetCharValue(key) == 'k'),
            (_, _) =>
            {
                View? target = above();
                FocusView(target);
                if (target is InteractiveTextField targetField)
                {
                    targetField.EnterEditMode();
                }

                return true;
            }));

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => !f.IsEditing && (key == Key.CursorDown || KeyHelpers.GetCharValue(key) == 'j'),
            (_, _) =>
            {
                View? target = below();
                FocusView(target);
                if (target is InteractiveTextField targetField)
                {
                    targetField.EnterEditMode();
                }

                return true;
            }));

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => f.IsEditing && key == Key.Enter,
            (f, _) =>
            {
                f.ExitEditMode();
                View? next = below();
                FocusView(next);
                if (next is InteractiveTextField nextField)
                {
                    nextField.EnterEditMode();
                }

                return true;
            }));

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => f.IsEditing && key == Key.Esc,
            (f, _) =>
            {
                f.ExitEditMode();
                return true;
            }));
    }

    private static void InvokeSideAction(InteractiveTextField field, FormFieldSideAction action)
    {
        string? result = action.Invoke(field.Text);
        if (!string.IsNullOrWhiteSpace(result))
        {
            field.Text = result;
            field.MoveEnd();
        }
    }

    private void TrySave()
    {
        HideInputError();

        for (int i = 0; i < Fields.Count; i++)
        {
            FormFieldSpec spec = Fields[i];
            string value = _fields[i].Text.Trim();

            if (spec.Required && string.IsNullOrWhiteSpace(value))
            {
                ShowInputError($"[warning]{spec.Label}[/] cannot be empty");
                FocusView(_fields[i]);
                _fields[i].EnterEditMode();
                return;
            }

            string? validationError = spec.Validate?.Invoke(value);
            if (validationError is not null)
            {
                ShowInputError(validationError);
                FocusView(_fields[i]);
                _fields[i].EnterEditMode();
                return;
            }
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Fields.Count; i++)
        {
            result[Fields[i].Key] = _fields[i].Text.Trim();
        }

        Submitted?.Invoke(result);
    }

    private void FocusView(View? view)
    {
        view?.SetFocus();
    }

    private void ApplyFieldTheme(InteractiveTextField field, Color background, Color foreground)
    {
        field.ApplyTheme(background, foreground);

        if (Theme is null)
        {
            return;
        }

        Color idle = ColorResolver.Resolve(Theme.Secondary);
        Color focus = ColorResolver.Resolve(Theme.Primary);
        Color edit = ColorResolver.Resolve(Theme.OnSurface);

        field.SetBorderColors(idle, focus, edit, background);
    }

    private void ShowInputError(string message)
    {
        if (_inputErrorLabel is null)
        {
            return;
        }

        _inputErrorLabel.Markup = message;
        _inputErrorLabel.Visible = true;
    }

    private void HideInputError()
    {
        _inputErrorLabel?.Visible = false;
    }
}
