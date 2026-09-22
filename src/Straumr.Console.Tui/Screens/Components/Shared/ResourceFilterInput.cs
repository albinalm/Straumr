using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class ResourceFilterInput : PromptEditor
{
    public ResourceFilterInput(PromptEditorConfig config)
        : base(config)
    {
        Focusable = false;
    }

    public char? PendingEcho { get; set; }

    public Action<string>? TextChanged { get; set; }

    public void ReportTextChanged() => TextChanged?.Invoke(Text ?? string.Empty);

    public void Activate()
    {
        Focusable = true;
        App?.Focus(this);
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
        TextChanged?.Invoke(Text ?? string.Empty);
    }

    protected override void OnPointerPressed(PointerEventArgs e)
    {
        if (!Focusable)
        {
            Activate();
        }

        base.OnPointerPressed(e);
    }
}
