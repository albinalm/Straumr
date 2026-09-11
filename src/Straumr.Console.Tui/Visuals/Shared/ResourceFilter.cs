using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Visuals.Shared;

internal sealed class ResourceFilter
{
    private const char Prefix = '/';

    private static readonly PromptEditorConfig Config = PromptEditorConfig.Default with
    {
        AcceptCommand = PromptEditorConfig.Default.AcceptCommand with
        {
            LabelMarkup = "Apply"
        },
        CancelCommand = PromptEditorConfig.Default.CancelCommand with
        {
            LabelMarkup = "Clear"
        },
        InsertNewLineCommand = PromptEditorConfig.Default.InsertNewLineCommand with
        {
            Gesture = null
        },
        CompleteCommand = PromptEditorConfig.Default.CompleteCommand with
        {
            Gesture = null
        },
        HistoryPreviousCommand = PromptEditorConfig.Default.HistoryPreviousCommand with
        {
            Gesture = null
        },
        HistoryNextCommand = PromptEditorConfig.Default.HistoryNextCommand with
        {
            Gesture = null
        }
    };

    private readonly FilterInput _editor;
    private readonly Func<Visual?> _resultFocusTarget;

    public ResourceFilter(
        string placeholder,
        Action<string> textChanged,
        Func<Visual?> resultFocusTarget)
    {
        _resultFocusTarget = resultFocusTarget;
        _editor = new FilterInput(Config)
        {
            PromptMarkup = Prefix.ToString(),
            Placeholder = placeholder,
            LineMode = PromptEditorLineMode.SingleLine,
            EnableGhostCompletion = false,
            EnableWordHints = false,
            IsTabStop = false,
            HorizontalAlignment = Align.Stretch
        };
        _editor.SetStyle(StraumrStyles.CommandPrompt);
        _editor.Highlighter = new Delegator<PromptEditorHighlighter>(Highlight);
        _editor.TextChanged = textChanged;
        _editor.RemoveCommand("TextEditor.Undo");
        _editor.RemoveCommand("TextEditor.Redo");
        _editor.AcceptedRouted += (_, _) => FocusResults();
        _editor.CanceledRouted += (_, _) =>
        {
            _editor.Text = string.Empty;
            FocusResults();
        };
    }

    public Visual Root => _editor;

    public string Text => _editor.Text ?? string.Empty;

    public void Clear() => _editor.Text = string.Empty;

    public void AttachCommands(Visual target)
    {
        target.AddCommand(BuildOpenCommand(
            "ResourceFilter.Open",
            TerminalModifiers.None,
            CommandPresentation.CommandBar));
        target.AddCommand(BuildOpenCommand(
            "ResourceFilter.Open.Shifted",
            TerminalModifiers.Shift,
            CommandPresentation.None));
    }

    private Command BuildOpenCommand(
        string id,
        TerminalModifiers modifiers,
        CommandPresentation presentation) =>
        new()
        {
            Id = id,
            LabelMarkup = "Filter",
            Gesture = new KeyGesture(Prefix, modifiers),
            Importance = CommandImportance.Primary,
            Presentation = presentation,
            CanExecute = _ => !ReferenceEquals(_editor.App?.FocusedElement, _editor),
            IsVisible = _ => !ReferenceEquals(_editor.App?.FocusedElement, _editor),
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => Open()
        };

    private void Open()
    {
        _editor.PendingEcho = Prefix;
        _editor.CaretIndex = Text.Length;
        _editor.Activate();
    }

    private void FocusResults()
    {
        Visual? target = _resultFocusTarget();
        if (target is not null)
            _editor.App?.Focus(target);
    }

    private static void Highlight(in PromptEditorHighlightRequest request, List<StyledRun> runs) =>
        runs.Add(new StyledRun(0, request.Snapshot.Length, StraumrStyles.CommandPromptText));

    private sealed class FilterInput : PromptEditor
    {
        public FilterInput(PromptEditorConfig config)
            : base(config)
        {
            Focusable = false;
        }

        public char? PendingEcho { get; set; }

        public Action<string>? TextChanged { get; set; }

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
                Activate();

            base.OnPointerPressed(e);
        }
    }
}
