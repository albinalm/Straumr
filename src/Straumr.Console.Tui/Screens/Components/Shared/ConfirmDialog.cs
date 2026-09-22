using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class ConfirmDialog
{
    private const int DialogWidth = 58;
    private readonly Button _cancelButton;

    private readonly Dialog _dialog;

    public ConfirmDialog(
        string title,
        string question,
        string detail,
        string confirmLabel,
        bool destructive,
        Action confirm,
        Action? cancel = null,
        KeyGesture? cancelGesture = null)
    {
        var cancelButton = new Button("Cancel")
        {
            AutoFocus = true
        };
        cancelButton.SetStyle(StraumrStyleService.Button);

        var confirmButton = new Button(confirmLabel);
        confirmButton.SetStyle(destructive ? StraumrStyleService.DangerButton : StraumrStyleService.PrimaryButton);

        cancelButton.KeyDown((_, e) => MoveToOtherAnswer(e, confirmButton));
        confirmButton.KeyDown((_, e) => MoveToOtherAnswer(e, cancelButton));

        VStack content = new VStack(
                new TextBlock(question).Style(StraumrStyleService.BrightText).Wrap(true),
                new TextBlock(detail).Style(StraumrStyleService.MutedText).Wrap(true),
                new HStack(cancelButton, confirmButton)
                    .Spacing(1)
                    .HorizontalAlignment(Align.End))
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        _dialog = StraumrDialogHelpers.Create(
            new TextBlock(title).Style(destructive ? StraumrStyleService.RedText : StraumrStyleService.AccentText),
            content,
            DialogWidth);

        _cancelButton = cancelButton;
        cancelButton.Click(() => Answer(cancel));
        confirmButton.Click(() => Answer(confirm));

        if (cancel is null)
        {
            return;
        }

        _dialog.RemoveCommand(StraumrDialogHelpers.CancelCommandId);
        _dialog.AddCommand(new Command
        {
            Id = "ConfirmDialog.Cancel",
            LabelMarkup = "Cancel",
            Gesture = cancelGesture ?? TuiKeybindHelpers.Get("ConfirmDialog.Cancel"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => Answer(cancel)
        });
    }

    private void Answer(Action? answer)
    {
        _dialog.Close();
        answer?.Invoke();
    }

    public void Show() =>
        TuiWindowHelpers.Show(_dialog, () => _cancelButton.App?.Focus(_cancelButton));

    private static void MoveToOtherAnswer(KeyEventArgs e, Button otherAnswer)
    {
        if (!TuiKeybindHelpers.Matches("ConfirmDialog.Left", e) && !TuiKeybindHelpers.Matches("ConfirmDialog.Right", e) &&
            !TuiKeybindHelpers.Matches("ConfirmDialog.Up", e) && !TuiKeybindHelpers.Matches("ConfirmDialog.Down", e))
        {
            return;
        }

        otherAnswer.App?.Focus(otherAnswer);
        e.Handled = true;
    }
}
