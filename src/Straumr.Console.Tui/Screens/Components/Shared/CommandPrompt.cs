using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

public sealed class CommandPrompt
{
    private const char Prefix = ':';

    private const string CloseGlyph = "✕";

    private readonly Func<string, int, TuiCommandCompletionModel> _complete;
    private readonly CommandPromptInput _editor;
    private readonly State<bool> _isOpen = new(false);
    private readonly CommandPromptRow _row;
    private readonly Action<string> _submit;
    private int _applied;
    private int _appliedCaret;
    private string _appliedText = string.Empty;
    private int _candidateStart;
    private string[] _candidates = [];
    private Visual? _focusBeforeOpen;

    public CommandPrompt(Func<string, int, TuiCommandCompletionModel> complete, Action<string> submit)
    {
        _complete = complete;
        _submit = submit;

        _editor = new CommandPromptInput(Config)
        {
            PromptMarkup = $" {Prefix}",
            LineMode = PromptEditorLineMode.SingleLine,
            CompletionPresentation = PromptEditorCompletionPresentation.InlineCycle,
            EnableWordHints = false,
            History = new PromptEditorHistory(),
            HorizontalAlignment = Align.Stretch,
            Margin = new Thickness(0, 0, 1, 0)
        };
        _editor.SetStyle(StraumrStyleService.CommandPrompt);
        _editor.CompletionHandler = new Delegator<PromptEditorCompletionHandler>(Complete);
        _editor.Highlighter = new Delegator<PromptEditorHighlighter>(Highlight);
        _editor.Escaped = Close;
        _editor.AcceptedRouted += (_, e) => Accept(e.Text);
        _editor.CanceledRouted += (_, _) => Close();

        var close = new ChromeButton(CloseGlyph);
        close.SetStyle(StraumrStyleService.PrimaryButton);
        close.Click(Close);

        _row = new CommandPromptRow(_editor, close) { IsVisible = false };
    }

    private static PromptEditorConfig Config => PromptEditorConfig.Default with
    {
        HistoryPreviousCommand = PromptEditorConfig.Default.HistoryPreviousCommand with
        {
            Gesture = TuiKeybindHelpers.Get("CommandPrompt.HistoryPrevious")
        },
        HistoryNextCommand = PromptEditorConfig.Default.HistoryNextCommand with
        {
            Gesture = TuiKeybindHelpers.Get("CommandPrompt.HistoryNext")
        },
        AcceptCommand = PromptEditorConfig.Default.AcceptCommand with
        {
            Gesture = TuiKeybindHelpers.Get("CommandPrompt.Accept")
        },
        CompleteCommand = PromptEditorConfig.Default.CompleteCommand with
        {
            Gesture = TuiKeybindHelpers.Get("CommandPrompt.Complete")
        },
        CancelCommand = PromptEditorConfig.Default.CancelCommand with
        {
            Gesture = null
        }
    };

    public Visual Root => _row;

    public bool IsOpen => _isOpen.Value;

    public void Open()
    {
        if (_isOpen.Value)
        {
            return;
        }

        TerminalApp? app = _editor.App;
        _focusBeforeOpen = app?.FocusedElement;
        _editor.Text = string.Empty;
        _editor.PendingEcho = TuiKeybindHelpers.Echo("Straumr.OpenCommandPrompt");
        _row.IsVisible = true;
        _isOpen.Value = true;
        app?.Focus(_editor);
    }

    public void Close()
    {
        if (!_isOpen.Value)
        {
            return;
        }

        _isOpen.Value = false;
        _row.IsVisible = false;
        _editor.PendingEcho = null;
        _candidates = [];
        _editor.Text = string.Empty;

        TerminalApp? app = _editor.App;
        if (app is not null && ReferenceEquals(app.FocusedElement, _editor))
        {
            app.Focus(_focusBeforeOpen);
        }

        _focusBeforeOpen = null;
    }

    public void CloseIfFocusLost()
    {
        if (!_isOpen.Value)
        {
            return;
        }

        TerminalApp? app = _editor.App;
        if (app is not null && !ReferenceEquals(app.FocusedElement, _editor))
        {
            Close();
        }
    }

    private void Accept(string text)
    {
        Close();
        _submit(text);
    }

    private static void Highlight(in PromptEditorHighlightRequest request, List<StyledRun> runs) =>
        runs.Add(new StyledRun(0, request.Snapshot.Length, StraumrStyleService.CommandPromptText));

    private PromptEditorCompletion Complete(in PromptEditorCompletionRequest request)
    {
        string text = _editor.Text ?? string.Empty;

        if (_candidates.Length > 1 && text == _appliedText && request.CaretIndex == _appliedCaret)
        {
            return Apply(text, (_applied + 1) % _candidates.Length, _candidates[_applied].Length);
        }

        TuiCommandCompletionModel completion = _complete(text, request.CaretIndex);
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
}
