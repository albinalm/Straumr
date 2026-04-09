using System.Text;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

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

    private PromptTextField? _textField;
    private Label? _errorLabel;
    private bool _confirming;

    public override View Build()
    {
        FrameView frame = CreateFrame(Title);

        _textField = new PromptTextField(OnTextChanged, TrySubmit, RequestCancel)
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Text = InitialValue ?? string.Empty,
            Secret = IsSecret,
        };

        _errorLabel = new Label
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Visible = false,
        };

        frame.Add(_textField, _errorLabel);

        // Capture y/n during confirmation mode
        frame.KeyDown += (_, key) =>
        {
            if (!_confirming)
            {
                return;
            }

            key.Handled = true;
            Rune rune = key.AsRune;

            if (rune.Value is 'y' or 'Y')
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
}
