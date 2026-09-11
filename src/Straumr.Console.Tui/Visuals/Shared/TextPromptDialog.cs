using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// A modal asking for one line of text, with local validation shown under the field. The folder
/// browser's create and rename both ask exactly this, differing only in their wording and in what
/// they consider a valid answer.
/// </summary>
internal sealed class TextPromptDialog
{
    private const int DialogWidth = 56;

    private readonly Dialog _dialog;
    private readonly PromptTextBox _input;
    private readonly ValidationPresenter _field;
    private readonly Func<string, string?> _validate;
    private readonly Action<string> _submit;

    /// <param name="validate">
    /// Returns the message to show, or <see langword="null"/> when the value is acceptable. It runs on
    /// submission rather than per keystroke, so a half-typed name does not read as an error.
    /// </param>
    public TextPromptDialog(
        string title,
        string label,
        string initialValue,
        string confirmLabel,
        Func<string, string?> validate,
        Action<string> submit)
    {
        _validate = validate;
        _submit = submit;

        _input = new PromptTextBox
        {
            AutoFocus = true,
            Text = initialValue,
            CaretIndex = initialValue.Length,
            HorizontalAlignment = Align.Stretch
        };
        _input.SetStyle(StraumrStyles.TextBox);
        _input.RemoveCommand("TextEditor.Undo");
        _input.RemoveCommand("TextEditor.Redo");
        _field = new ValidationPresenter(_input)
        {
            Placement = ValidationPlacement.Below
        };
        _field.SetStyle(StraumrStyles.Validation);
        _input.Changed = () => _field.Message = null;

        var cancelButton = new Button("Cancel");
        cancelButton.SetStyle(StraumrStyles.Button);

        var confirmButton = new Button(confirmLabel);
        confirmButton.SetStyle(StraumrStyles.PrimaryButton);
        confirmButton.Click(TrySubmit);

        var content = new VStack(
                new TextBlock(label).Style(StraumrStyles.MutedText),
                _field,
                new HStack(cancelButton, confirmButton)
                    .Spacing(1)
                    .HorizontalAlignment(Align.End))
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        _dialog = StraumrDialog.Create(
            new TextBlock(title).Style(StraumrStyles.AccentText),
            content,
            DialogWidth);

        // Scoped to the field rather than the dialog: a framework command runs before the focused
        // control sees the key, so a dialog-wide Enter would submit while a button had focus.
        _input.AddCommand(new Command
        {
            Id = "TextPromptDialog.Submit",
            LabelMarkup = confirmLabel,
            Gesture = new KeyGesture(TerminalKey.Enter),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => TrySubmit()
        });

        cancelButton.Click(() => _dialog.Close());
    }

    /// <summary>Arms the field to discard the printable keystroke that opened this dialog.</summary>
    public char? PendingEcho
    {
        get => _input.PendingEcho;
        set => _input.PendingEcho = value;
    }

    public void Show() => _dialog.Show();

    private void TrySubmit()
    {
        string value = (_input.Text ?? string.Empty).Trim();
        if (_validate(value) is { } message)
        {
            _field.Message = new ValidationMessage(
                ValidationSeverity.Error,
                new TextBlock(message));
            _input.App?.Focus(_input);
            return;
        }

        _dialog.Close();
        _submit(value);
    }

    private sealed class PromptTextBox : TextBox
    {
        public char? PendingEcho { get; set; }

        public Action? Changed { get; set; }

        /// <remarks>
        /// A printable gesture arrives as a key event and an independent text event, so the key opens
        /// and focuses this field and the pair's text event then types itself into it.
        /// </remarks>
        protected override void OnTextInput(TextInputEventArgs e)
        {
            bool isEcho = PendingEcho is { } echo && e.Text == echo.ToString();
            PendingEcho = null;

            if (isEcho)
            {
                e.Handled = true;
                return;
            }

            base.OnTextInput(e);
        }

        protected override void OnDocumentChanged(TextDocumentChangedEventArgs e)
        {
            base.OnDocumentChanged(e);
            Changed?.Invoke();
        }
    }
}
