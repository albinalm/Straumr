using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class TextPromptDialog
{
    private const int DialogWidth = 56;

    private readonly Dialog _dialog;
    private readonly ValidationPresenter _field;
    private readonly FormTextBox _input;
    private readonly Action<string> _submit;
    private readonly Func<string, string?> _validate;

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

        _input = FormTextBox.Create();
        _input.AutoFocus = true;
        _input.SetText(initialValue);
        _input.CaretIndex = initialValue.Length;
        _field = new ValidationPresenter(_input)
        {
            Placement = ValidationPlacement.Below
        };
        _field.SetStyle(StraumrStyleService.Validation);
        _input.Changed = () => _field.Message = null;

        var cancelButton = new Button("Cancel");
        cancelButton.SetStyle(StraumrStyleService.Button);

        var confirmButton = new Button(confirmLabel);
        confirmButton.SetStyle(StraumrStyleService.PrimaryButton);
        confirmButton.Click(TrySubmit);

        VStack content = new VStack(
                new TextBlock(label).Style(StraumrStyleService.MutedText),
                _field,
                new HStack(cancelButton, confirmButton)
                    .Spacing(1)
                    .HorizontalAlignment(Align.End))
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        _dialog = StraumrDialogHelpers.Create(
            new TextBlock(title).Style(StraumrStyleService.AccentText),
            content,
            DialogWidth);

        _input.AddCommand(new Command
        {
            Id = "TextPromptDialog.Submit",
            LabelMarkup = confirmLabel,
            Gesture = TuiKeybindHelpers.Get("TextPromptDialog.Submit"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => TrySubmit()
        });

        cancelButton.Click(() => _dialog.Close());
    }

    public char? PendingEcho
    {
        get => _input.PendingEcho;
        set => _input.PendingEcho = value;
    }

    public void Show() => TuiWindowHelpers.Show(_dialog);

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
}
