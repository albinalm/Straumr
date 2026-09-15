using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// The single-line text field every form in the app is built from: the shared palette, no undo
/// stack competing for <c>Ctrl</c> keys, a change notification, and the ability to discard the
/// keystroke that opened the form it sits in.
/// </summary>
internal sealed class FormTextBox : TextBox
{
    /// <summary>Arms the field to discard the printable keystroke that opened its form.</summary>
    /// <remarks>
    /// A printable gesture arrives as a key event and an independent text event, so the key opens
    /// and focuses the form and the pair's text event then types itself into the first field.
    /// </remarks>
    public char? PendingEcho { get; set; }

    public Action? Changed { get; set; }

    /// <summary>
    /// Fills the field and leaves the caret after the value rather than in front of it.
    /// </summary>
    /// <remarks>
    /// Always through here and never through <c>Text</c>, because a field entered by <c>Tab</c> is
    /// one the reader means to continue, and a caret at the front makes them travel the length of
    /// their own value first. It has to be done at the point of writing: <c>TextBox.Text</c> is a
    /// plain bindable property that the document reads through to, so the document raises no change
    /// for a value the form assigned — only for one the reader typed — and there is no event to
    /// hang this off. Doing it here also leaves a pointer click placing the caret where it was
    /// clicked, and a field the reader returns to holding the place they left.
    /// </remarks>
    public void SetText(string? value)
    {
        Text = value;
        CaretIndex = (value ?? string.Empty).Length;
    }

    /// <param name="secret">
    /// Masks the value except while the field itself has focus. A credential is hidden from someone
    /// reading the screen over a shoulder, and readable to whoever is actually typing it — a field
    /// that is masked while being typed into cannot be checked without saving and reopening.
    /// </param>
    public static FormTextBox Create(string? placeholder = null, bool secret = false)
    {
        var box = new FormTextBox
        {
            Placeholder = placeholder,
            IsPassword = secret,
            PasswordRevealMode = secret ? PasswordRevealMode.WhileFocused : PasswordRevealMode.Always,
            HorizontalAlignment = Align.Stretch
        };
        box.SetStyle(StraumrStyles.TextBox);
        // Tab traversal tests a candidate's own IsVisible and not its ancestors', so a field on a
        // form page that is currently hidden stays in the rotation: it took a Tab, placed the caret
        // where it would have been if its page were showing, and accepted typing nobody could see.
        // Every focusable in this app has to answer for its whole subtree; see FocusScope.
        box.IsTabStop(box.IsReachable);
        box.RemoveCommand("TextEditor.Undo");
        box.RemoveCommand("TextEditor.Redo");
        return box;
    }

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
