using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class FormTextBox : TextBox
{
    private readonly SecretSuggestions _suggestions;

    public FormTextBox()
    {
        _suggestions = new SecretSuggestions(this);
        Root = new VStack(this, _suggestions.Root).HorizontalAlignment(Align.Stretch);
    }

    public Visual Root { get; }

    public char? PendingEcho { get; set; }

    public Action? Changed { get; set; }

    public void SetText(string? value)
    {
        Text = value;
        CaretIndex = (value ?? string.Empty).Length;
    }

    public static FormTextBox Create(string? placeholder = null, bool secret = false)
    {
        var box = new FormTextBox
        {
            Placeholder = placeholder,
            IsPassword = secret,
            PasswordRevealMode = secret ? PasswordRevealMode.WhileFocused : PasswordRevealMode.Always,
            HorizontalAlignment = Align.Stretch
        };
        box.SetStyle(StraumrStyleService.TextBox);
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

    protected override void OnEditorStateChanged()
    {
        base.OnEditorStateChanged();
        _suggestions.Sync();
    }
}
