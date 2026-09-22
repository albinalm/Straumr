using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class ResourceFilter
{
    private const char Prefix = '/';

    private readonly ResourceFilterInput _editor;
    private readonly Func<Visual?> _resultFocusTarget;

    public ResourceFilter(
        string placeholder,
        Action<string> textChanged,
        Func<Visual?> resultFocusTarget)
    {
        _resultFocusTarget = resultFocusTarget;
        _editor = new ResourceFilterInput(Config)
        {
            PromptMarkup = Prefix.ToString(),
            Placeholder = placeholder,
            LineMode = PromptEditorLineMode.SingleLine,
            EnableGhostCompletion = false,
            EnableWordHints = false,
            IsTabStop = false,
            HorizontalAlignment = Align.Stretch
        };
        _editor.SetStyle(StraumrStyleService.CommandPrompt);
        _editor.Highlighter = new Delegator<PromptEditorHighlighter>(Highlight);
        _editor.TextChanged = textChanged;
        _editor.RemoveCommand("TextEditor.Undo");
        _editor.RemoveCommand("TextEditor.Redo");
        _editor.AcceptedRouted += (_, _) => FocusResults();
        _editor.CanceledRouted += (_, _) =>
        {
            Clear();
            FocusResults();
        };
    }

    private static PromptEditorConfig Config => PromptEditorConfig.Default with
    {
        AcceptCommand = PromptEditorConfig.Default.AcceptCommand with
        {
            LabelMarkup = "Apply",
            Gesture = TuiKeybindHelpers.Get("ResourceFilter.Accept")
        },
        CancelCommand = PromptEditorConfig.Default.CancelCommand with
        {
            LabelMarkup = "Clear",
            Gesture = TuiKeybindHelpers.Get("ResourceFilter.Cancel")
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

    public Visual Root => _editor;

    public string Text => _editor.Text ?? string.Empty;

    public void Clear()
    {
        _editor.Text = string.Empty;
        _editor.ReportTextChanged();
    }

    public void AttachCommands(Visual target)
    {
        target.AddCommand(BuildOpenCommand(
            "ResourceFilter.Open",
            CommandPresentation.CommandBar));
        target.AddCommand(BuildOpenCommand(
            "ResourceFilter.Open.Shifted",
            CommandPresentation.None));
    }

    private Command BuildOpenCommand(
        string id,
        CommandPresentation presentation) =>
        new()
        {
            Id = id,
            LabelMarkup = "Filter",
            Gesture = TuiKeybindHelpers.Get(id),
            Importance = CommandImportance.Primary,
            Presentation = presentation,
            CanExecute = _ => !ReferenceEquals(_editor.App?.FocusedElement, _editor),
            IsVisible = _ => !ReferenceEquals(_editor.App?.FocusedElement, _editor),
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => Open()
        };

    private void Open()
    {
        _editor.PendingEcho = TuiKeybindHelpers.Echo("ResourceFilter.Open");
        _editor.CaretIndex = Text.Length;
        _editor.Activate();
    }

    private void FocusResults()
    {
        Visual? target = _resultFocusTarget();
        if (target is not null)
        {
            _editor.App?.Focus(target);
        }
    }

    private static void Highlight(in PromptEditorHighlightRequest request, List<StyledRun> runs) =>
        runs.Add(new StyledRun(0, request.Snapshot.Length, StraumrStyleService.CommandPromptText));
}
