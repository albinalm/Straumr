using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Workspace;

internal sealed class WorkspaceFormDialog
{
    private readonly Dialog _dialog;
    private readonly FormTextBox _nameInput;
    private readonly FormTextBox _locationInput;
    private readonly ValidationPresenter _nameField;
    private readonly Action<WorkspaceFormSubmission> _submit;
    private readonly string? _defaultLocation;

    /// <summary>Leads the resolved location, beside the field that overrides it.</summary>
    private const string ResolvedGlyph = "→ ";

    /// <summary>
    /// The typed location, mirrored into state so the line under the field re-reads it. A computed
    /// visual only re-evaluates when something invalidates it, and a sibling text box taking a
    /// keystroke is not that.
    /// </summary>
    private readonly State<string> _locationText = new(string.Empty);

    public WorkspaceFormDialog(
        string title,
        string actionLabel,
        string? sourceName,
        string? defaultLocation,
        char openingGesture,
        Action<WorkspaceFormSubmission> submit)
    {
        _submit = submit;
        _defaultLocation = defaultLocation;
        _nameInput = new FormTextBox
        {
            AutoFocus = true,
            Placeholder = "workspace name",
            HorizontalAlignment = Align.Stretch
        };
        _nameInput.SetStyle(StraumrStyles.TextBox);
        _nameInput.PendingEcho = openingGesture;
        _nameInput.RemoveCommand("TextEditor.Undo");
        _nameInput.RemoveCommand("TextEditor.Redo");

        // The placeholder is a short hint rather than the path itself. A TextBox has no trimming
        // control, so a long path filled the field head-first and cut the tail — the half that says
        // which folder it is. The resolved location goes on its own line below, trimmed from the
        // front so the tail survives.
        _locationInput = new FormTextBox
        {
            Placeholder = defaultLocation is null ? "workspace directory" : "default location",
            HorizontalAlignment = Align.Stretch
        };
        _locationInput.SetStyle(StraumrStyles.TextBox);
        _locationInput.RemoveCommand("TextEditor.Undo");
        _locationInput.RemoveCommand("TextEditor.Redo");
        _locationInput.Changed = () => _locationText.Value = _locationInput.Text ?? string.Empty;

        var browseButton = new Button("Browse");
        browseButton.SetStyle(StraumrStyles.Button);
        browseButton.Click(ShowFolderBrowser);

        var location = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star() },
                new ColumnDefinition { Width = GridLength.Auto })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(1)
            .Cell(_locationInput, 0, 0)
            .Cell(browseButton, 0, 1)
            .Cell(BuildResolvedLocation(), 1, 0)
            .HorizontalAlignment(Align.Stretch);

        _nameField = new ValidationPresenter(_nameInput)
        {
            Placement = ValidationPlacement.Below
        };
        _nameField.SetStyle(StraumrStyles.Validation);
        _nameInput.Changed = () => _nameField.Message = null;

        var cancelButton = new Button("Cancel");
        cancelButton.SetStyle(StraumrStyles.Button);

        var actionButton = new Button(actionLabel);
        actionButton.SetStyle(StraumrStyles.PrimaryButton);

        var actions = new HStack(cancelButton, actionButton)
            .Spacing(1)
            .HorizontalAlignment(Align.End);

        var fields = new VStack(
                new TextBlock("Name").Style(StraumrStyles.MutedText),
                _nameField,
                new TextBlock("Location (optional)").Style(StraumrStyles.MutedText),
                location)
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        var content = new VStack()
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);
        if (sourceName is not null)
        {
            content.Add(new HStack(
                    new TextBlock("Source").Style(StraumrStyles.MutedText),
                    new TextBlock(sourceName).Style(StraumrStyles.PrimaryText))
                .Spacing(2));
        }

        content.Add(fields);
        content.Add(actions);

        _dialog = StraumrDialog.Create(
            new TextBlock(title).Style(StraumrStyles.AccentText),
            content,
            64);
        AddSubmitCommand(_nameInput, actionLabel);
        AddSubmitCommand(_locationInput, actionLabel);

        cancelButton.Click(() => _dialog.Close());
        actionButton.Click(TrySubmit);
    }

    public void Show() => _dialog.Show();

    /// <remarks>
    /// The glyph keeps a column of its own: the path beside it is trimmed from the front, and a
    /// marker at the head of that text would be the first thing the ellipsis ate.
    /// </remarks>
    private Visual BuildResolvedLocation() =>
        new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Cell(new TextBlock(ResolvedGlyph).Style(StraumrStyles.MutedText), 0, 0)
            .Cell(
                new TextBlock(ResolvedLocationText)
                    .Style(() => ResolvedLocation is null
                        ? StraumrStyles.AmberText
                        : StraumrStyles.MutedBrightText)
                    // A path is trimmed from the front so its tail survives; the warning that
                    // replaces it is a sentence, and trimming that from the front eats its meaning.
                    .Trimming(() => ResolvedLocation is null
                        ? TextTrimming.EndEllipsis
                        : TextTrimming.StartEllipsis)
                    .HorizontalAlignment(Align.Stretch),
                0,
                1)
            .HorizontalAlignment(Align.Stretch);

    /// <summary>
    /// Where the workspace will actually be written: what was typed, or the location the caller
    /// offered when the field is left blank. Both the line under the field and the submission read
    /// this, so what is shown and what happens cannot drift apart.
    /// </summary>
    private string? ResolvedLocation
    {
        get
        {
            string typed = _locationText.Value.Trim();
            return typed.Length > 0 ? typed : _defaultLocation;
        }
    }

    /// <summary>Writes the field and the state it is mirrored into together, so neither leads.</summary>
    private void SetLocation(string path)
    {
        _locationInput.Text = path;
        _locationText.Value = path;
    }

    /// <remarks>
    /// With nothing configured and nothing typed, Core has no root to compose a path from and the
    /// operation would fail on submission. Saying so here is cheaper than reporting it afterwards.
    /// </remarks>
    private string ResolvedLocationText() =>
        ResolvedLocation is { } location
            ? PathFormatting.Display(location)
            : "No default location. Choose one with Browse.";

    private void AddSubmitCommand(Visual target, string actionLabel) =>
        target.AddCommand(new Command
        {
            Id = "WorkspaceFormDialog.Submit",
            LabelMarkup = actionLabel,
            Gesture = new KeyGesture(TerminalKey.Enter),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => TrySubmit()
        });

    private void ShowFolderBrowser() =>
        new FolderBrowserDialog(
            _locationText.Value,
            _defaultLocation,
            SetLocation)
        .Show();

    private void TrySubmit()
    {
        string name = (_nameInput.Text ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            _nameField.Message = new ValidationMessage(
                ValidationSeverity.Error,
                new TextBlock("Name is required."));
            _nameInput.App?.Focus(_nameInput);
            return;
        }

        // Left blank means the location the placeholder is showing. Returning null instead would
        // send Core to the configured default, which is not what the field said would happen the
        // moment a caller offers a location of its own.
        _dialog.Close();
        _submit(new WorkspaceFormSubmission(name, ResolvedLocation));
    }

    private sealed class FormTextBox : TextBox
    {
        public char? PendingEcho { get; set; }

        public Action? Changed { get; set; }

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
}

internal sealed record WorkspaceFormSubmission(string Name, string? OutputDirectory);
