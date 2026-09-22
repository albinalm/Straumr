using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class CommandPromptInput(PromptEditorConfig config) : PromptEditor(config)
{
    public char? PendingEcho { get; set; }

    public Action? Escaped { get; set; }

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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!TuiKeybindHelpers.Matches("CommandPrompt.Cancel", e))
        {
            base.OnKeyDown(e);
            return;
        }

        Cancel();
        Escaped?.Invoke();
        e.Handled = true;
    }
}
