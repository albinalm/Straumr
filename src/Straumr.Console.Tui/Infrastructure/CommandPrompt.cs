using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class CommandPrompt
{
    private const char Prefix = ':';

    private static readonly PromptEditorConfig Config = PromptEditorConfig.Default with
    {
        HistoryPreviousCommand = PromptEditorConfig.Default.HistoryPreviousCommand with
        {
            Gesture = new KeyGesture(TerminalKey.Up)
        },
        HistoryNextCommand = PromptEditorConfig.Default.HistoryNextCommand with
        {
            Gesture = new KeyGesture(TerminalKey.Down)
        },
        CancelCommand = PromptEditorConfig.Default.CancelCommand with
        {
            Gesture = null
        }
    };

    private readonly Func<string, int, TuiCommandCompletion> _complete;
    private readonly Action<string> _submit;
    private readonly PromptInput _editor;
    private readonly State<bool> _isOpen = new(false);
    private string[] _candidates = [];
    private int _candidateStart;
    private int _applied;
    private string _appliedText = string.Empty;
    private int _appliedCaret;
    private Visual? _focusBeforeOpen;

    public CommandPrompt(Func<string, int, TuiCommandCompletion> complete, Action<string> submit)
    {
        _complete = complete;
        _submit = submit;

        _editor = new PromptInput(Config)
        {
            PromptMarkup = $" {Prefix}",
            LineMode = PromptEditorLineMode.SingleLine,
            CompletionPresentation = PromptEditorCompletionPresentation.InlineCycle,
            EnableWordHints = false,
            History = new PromptEditorHistory(),
            HorizontalAlignment = Align.Stretch,
            Margin = new Thickness(0, 0, 1, 0),
            IsVisible = false
        };
        _editor.SetStyle(StraumrStyles.CommandPrompt);
        _editor.CompletionHandler = new Delegator<PromptEditorCompletionHandler>(Complete);
        _editor.Highlighter = new Delegator<PromptEditorHighlighter>(Highlight);
        _editor.Escaped = Close;
        _editor.AcceptedRouted += (_, e) => Accept(e.Text);
        _editor.CanceledRouted += (_, _) => Close();
    }

    public Visual Root => _editor;

    public bool IsOpen => _isOpen.Value;

    public void Open()
    {
        if (_isOpen.Value)
            return;

        TerminalApp? app = _editor.App;
        _focusBeforeOpen = app?.FocusedElement;
        _editor.Text = string.Empty;
        _editor.PendingEcho = Prefix;
        _editor.IsVisible = true;
        _isOpen.Value = true;
        app?.Focus(_editor);
    }

    public void Close()
    {
        if (!_isOpen.Value)
            return;

        _isOpen.Value = false;
        _editor.IsVisible = false;
        _editor.PendingEcho = null;
        _candidates = [];
        _editor.Text = string.Empty;

        TerminalApp? app = _editor.App;
        if (app is not null && ReferenceEquals(app.FocusedElement, _editor))
            app.Focus(_focusBeforeOpen);

        _focusBeforeOpen = null;
    }

    public void CloseIfFocusLost()
    {
        if (!_isOpen.Value)
            return;

        TerminalApp? app = _editor.App;
        if (app is not null && !ReferenceEquals(app.FocusedElement, _editor))
            Close();
    }

    private void Accept(string text)
    {
        Close();
        _submit(text);
    }

    private static void Highlight(in PromptEditorHighlightRequest request, List<StyledRun> runs) =>
        runs.Add(new StyledRun(0, request.Snapshot.Length, StraumrStyles.CommandPromptText));

    private PromptEditorCompletion Complete(in PromptEditorCompletionRequest request)
    {
        string text = _editor.Text ?? string.Empty;

        if (_candidates.Length > 1 && text == _appliedText && request.CaretIndex == _appliedCaret)
            return Apply(text, (_applied + 1) % _candidates.Length, _candidates[_applied].Length);

        TuiCommandCompletion completion = _complete(text, request.CaretIndex);
        if (completion.Candidates.Count == 0)
        {
            _candidates = [];
            return new PromptEditorCompletion(false, [], 0, 0, 0, string.Empty);
        }

        _candidates = [.. completion.Candidates];
        _candidateStart = completion.ReplaceStart;
        return Apply(text, 0, completion.ReplaceLength);
    }

    private PromptEditorCompletion Apply(string text, int index, int replaceLength)
    {
        string candidate = _candidates[index];
        _applied = index;
        _appliedText = string.Concat(
            text.AsSpan(0, _candidateStart),
            candidate,
            text.AsSpan(_candidateStart + replaceLength));
        _appliedCaret = _candidateStart + candidate.Length;

        return new PromptEditorCompletion(
            true,
            _candidates,
            _candidateStart,
            replaceLength,
            index,
            string.Empty);
    }

    private sealed class PromptInput(PromptEditorConfig config) : PromptEditor(config), IModalVisual
    {
        public bool IsModal => IsVisible;

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
            if (e.Key != TerminalKey.Escape)
            {
                base.OnKeyDown(e);
                return;
            }

            Cancel();
            Escaped?.Invoke();
            e.Handled = true;
        }
    }
}
