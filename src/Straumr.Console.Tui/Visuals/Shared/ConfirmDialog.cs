using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// The modal asking one irreversible question. Cancel holds initial focus, so the answer a stray
/// <c>Enter</c> gives is the one that changes nothing.
/// </summary>
internal sealed class ConfirmDialog
{
    private const int DialogWidth = 58;

    private readonly Dialog _dialog;
    private readonly Button _cancelButton;

    /// <param name="destructive">
    /// Whether confirming loses something. It paints the title and the confirming button red, which
    /// is the only cue separating "this cannot be undone" from an ordinary question.
    /// </param>
    public ConfirmDialog(
        string title,
        string question,
        string detail,
        string confirmLabel,
        bool destructive,
        Action confirm)
    {
        var cancelButton = new Button("Cancel")
        {
            AutoFocus = true
        };
        cancelButton.SetStyle(StraumrStyles.Button);

        var confirmButton = new Button(confirmLabel);
        confirmButton.SetStyle(destructive ? StraumrStyles.DangerButton : StraumrStyles.PrimaryButton);

        // A confirmation is one choice expressed by two buttons, so the direction keys move within
        // that choice rather than doing nothing. Tab remains available and Enter still activates
        // whichever answer has focus; arrows only change the focused answer.
        cancelButton.KeyDown((_, e) => MoveToOtherAnswer(e, confirmButton));
        confirmButton.KeyDown((_, e) => MoveToOtherAnswer(e, cancelButton));

        var content = new VStack(
                new TextBlock(question).Style(StraumrStyles.BrightText).Wrap(true),
                new TextBlock(detail).Style(StraumrStyles.MutedText).Wrap(true),
                new HStack(cancelButton, confirmButton)
                    .Spacing(1)
                    .HorizontalAlignment(Align.End))
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        _dialog = StraumrDialog.Create(
            new TextBlock(title).Style(destructive ? StraumrStyles.RedText : StraumrStyles.AccentText),
            content,
            DialogWidth);

        _cancelButton = cancelButton;
        cancelButton.Click(() => _dialog.Close());
        confirmButton.Click(() =>
        {
            _dialog.Close();
            confirm();
        });
    }

    /// <remarks>
    /// Focus is taken here rather than left to <c>AutoFocus</c>, which the framework applies on the
    /// render that follows and only while nothing else holds focus. A dialog opened by a typed
    /// command is shown in the middle of an update pass that goes on to place focus itself, so one
    /// not focused until the next render is one the keyboard can be left behind.
    /// </remarks>
    public void Show()
    {
        _dialog.Show();
        _cancelButton.App?.Focus(_cancelButton);
    }

    private static void MoveToOtherAnswer(KeyEventArgs e, Button otherAnswer)
    {
        if (e.Key is not (TerminalKey.Left or TerminalKey.Right or TerminalKey.Up or TerminalKey.Down))
            return;

        otherAnswer.App?.Focus(otherAnswer);
        e.Handled = true;
    }
}
