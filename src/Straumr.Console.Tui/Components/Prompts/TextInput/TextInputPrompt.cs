using Straumr.Console.Tui.Components.Prompts.Base;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Factories;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using MarkupText = Straumr.Console.Tui.Helpers.MarkupText;

namespace Straumr.Console.Tui.Components.Prompts.TextInput;

internal sealed class TextInputPrompt : PromptComponent
{
    public required string Title { get; init; }
    public string? InitialValue { get; init; }
    public bool AllowEmpty { get; init; }
    public bool IsSecret { get; init; }
    public Func<string, string?>? Validate { get; init; }

    public event Action<string>? Submitted;
    public event Action? CancelRequested;

    private InteractiveTextField? _textField;
    private Label? _errorLabel;
    private bool _confirming;

    public override View Build()
    {
        FrameView frame = CreateFrame(Title);

        _textField = TextFieldFactory.CreatePromptField(OnTextChanged);
        _textField.X = 1;
        _textField.Y = 1;
        _textField.Width = Dim.Fill(2);
        _textField.Text = InitialValue ?? string.Empty;
        _textField.Secret = IsSecret;

        ApplyTheme();

        _errorLabel = new Label
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Visible = false,
        };

        frame.Add(_textField, _errorLabel);
        
        frame.KeyDown += (_, key) =>
        {
            if (!_confirming)
            {
                return;
            }

            key.Handled = true;

            if (KeyHelpers.GetCharValue(key) is 'y' or 'Y')
            {
                _confirming = false;
                CancelRequested?.Invoke();
            }
            else
            {
                ExitConfirmMode();
            }
        };

        return frame;
    }

    internal bool HandleInputKeyDown(Key key)
    {
        if (_confirming && KeyHelpers.IsEscape(key))
        {
            return RequestCancel();
        }

        if (_confirming)
        {
            return false;
        }

        if (_textField is not { HasFocus: true })
        {
            return false;
        }

        if (KeyHelpers.IsEscape(key))
        {
            return RequestCancel();
        }

        if (KeyHelpers.IsEnter(key))
        {
            return TrySubmit();
        }

        return false;
    }

    public void FocusInput() => _textField?.SetFocus();

    private void OnTextChanged()
    {
        HideError();
        if (_confirming)
        {
            ExitConfirmMode();
        }
    }

    private bool TrySubmit()
    {
        if (_confirming)
        {
            ExitConfirmMode();
        }

        if (_textField is null)
        {
            return true;
        }

        string value = _textField.Text;

        if (!AllowEmpty && value.Length == 0)
        {
            ShowError("A value is required");
            return true;
        }

        string? validationError = Validate?.Invoke(value);
        if (validationError is not null)
        {
            ShowError(MarkupText.ToPlain(validationError));
            return true;
        }

        Submitted?.Invoke(value);
        return true;
    }

    private bool RequestCancel()
    {
        if (_confirming)
        {
            ExitConfirmMode();
            return true;
        }

        string current = _textField?.Text ?? string.Empty;
        string initial = InitialValue ?? string.Empty;

        bool isDirty = !string.Equals(current, initial, StringComparison.Ordinal);

        if (!isDirty)
        {
            CancelRequested?.Invoke();
            return true;
        }

        EnterConfirmMode();
        return true;
    }

    private void EnterConfirmMode()
    {
        _confirming = true;
        _textField?.Enabled = false;

        ShowError("Discard changes? (y/n)");
    }

    private void ExitConfirmMode()
    {
        _confirming = false;
        if (_textField is not null)
        {
            _textField.Enabled = true;
            _textField.SetFocus();
        }

        HideError();
    }

    private void HideError()
    {
        _errorLabel?.Visible = false;
    }

    private void ShowError(string message)
    {
        if (_errorLabel is null)
        {
            return;
        }

        _errorLabel.Text = message;
        _errorLabel.Visible = true;
    }

    private void ApplyTheme()
    {
        if (Theme is null || _textField is null)
        {
            return;
        }

        Color background = ColorResolver.Resolve(Theme.Surface);
        Color foreground = ColorResolver.Resolve(Theme.OnSurface);
        _textField.ApplyTheme(background, foreground);
    }
}
