using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class BrowserLocationInput : TextBox
{
    private readonly Func<string, IReadOnlyList<string>> _complete;
    private readonly Func<string> _currentPath;
    private readonly Action<string?> _showError;
    private string? _completedText;
    private int _completionIndex;
    private string[] _completions = [];

    public BrowserLocationInput(
        Func<string> currentPath,
        Action<string> navigate,
        Action close,
        Action<string?> showError,
        Func<string, IReadOnlyList<string>> complete)
    {
        _currentPath = currentPath;
        _showError = showError;
        _complete = complete;
        Focusable = false;

        AddCommand(new Command
        {
            Id = "BrowserDialog.Location.Go",
            LabelMarkup = "Go",
            Gesture = TuiKeybindHelpers.Get("BrowserDialog.Location.Go"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => navigate(Text ?? string.Empty)
        });
        AddCommand(new Command
        {
            Id = "BrowserDialog.Location.Cancel",
            LabelMarkup = "Back",
            Gesture = TuiKeybindHelpers.Get("BrowserDialog.Location.Cancel"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => close()
        });
    }

    public char? PendingEcho { get; set; }

    public void Activate()
    {
        // The framework rejects focus for a hidden control.
        IsVisible = true;
        Focusable = true;
        SetPath(_currentPath());
        App?.Focus(this);
    }

    public void Deactivate()
    {
        Focusable = false;
        IsVisible = false;
    }

    public void SetPath(string path)
    {
        Text = path;
        CaretIndex = path.Length;
        ResetCompletion();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (TuiKeybindHelpers.Matches("BrowserDialog.Location.Complete", e))
        {
            Complete();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
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
        _showError(null);
        if (!string.Equals(Text, _completedText, StringComparison.Ordinal))
        {
            ResetCompletion();
        }
    }

    private void Complete()
    {
        string text = Text ?? string.Empty;
        if (_completions.Length > 0 &&
            string.Equals(text, _completedText, StringComparison.Ordinal))
        {
            _completionIndex = (_completionIndex + 1) % _completions.Length;
            ApplyCompletion(_completions[_completionIndex]);
            return;
        }

        _completions = _complete(text).ToArray();

        if (_completions.Length == 0)
        {
            return;
        }

        _completionIndex = 0;
        ApplyCompletion(_completions[0]);
    }

    private void ApplyCompletion(string path)
    {
        Text = path;
        CaretIndex = path.Length;
        _completedText = path;
    }

    private void ResetCompletion()
    {
        _completions = [];
        _completedText = null;
        _completionIndex = 0;
    }
}
