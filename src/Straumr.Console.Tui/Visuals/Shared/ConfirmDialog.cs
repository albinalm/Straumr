using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// The modal asking one irreversible question. Cancel holds initial focus, so the answer a stray
/// <c>Enter</c> gives is the one that changes nothing.
/// </summary>
internal sealed class ConfirmDialog
{
    private const int DialogWidth = 58;

    private readonly Dialog _dialog;

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

        cancelButton.Click(() => _dialog.Close());
        confirmButton.Click(() =>
        {
            _dialog.Close();
            confirm();
        });
    }

    public void Show() => _dialog.Show();
}
