using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Workspace;

internal sealed class WorkspaceFormDialog
{

    private const string ResolvedGlyph = "→ ";
    private readonly string? _defaultLocation;
    private readonly Dialog _dialog;
    private readonly FormTextBox _locationInput;

    private readonly State<string> _locationText = new(string.Empty);
    private readonly ValidationPresenter _nameField;
    private readonly FormTextBox _nameInput;
    private readonly Action<WorkspaceFormSubmissionModel> _submit;

    public WorkspaceFormDialog(
        string title,
        string actionLabel,
        string? sourceName,
        string? defaultLocation,
        char openingGesture,
        Action<WorkspaceFormSubmissionModel> submit)
    {
        _submit = submit;
        _defaultLocation = defaultLocation;
        _nameInput = FormTextBox.Create("workspace name");
        _nameInput.AutoFocus = true;
        _nameInput.PendingEcho = TuiKeybindHelpers.OpeningEcho;

        _locationInput = FormTextBox.Create(
            defaultLocation is null ? "workspace directory" : "default location");
        _locationInput.Changed = () => _locationText.Value = _locationInput.Text ?? string.Empty;

        var browseButton = new Button("Browse");
        browseButton.SetStyle(StraumrStyleService.Button);
        browseButton.Click(ShowFolderBrowser);

        Grid location = new Grid()
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
        _nameField.SetStyle(StraumrStyleService.Validation);
        _nameInput.Changed = () => _nameField.Message = null;

        var cancelButton = new Button("Cancel");
        cancelButton.SetStyle(StraumrStyleService.Button);

        var actionButton = new Button(actionLabel);
        actionButton.SetStyle(StraumrStyleService.PrimaryButton);

        HStack actions = new HStack(cancelButton, actionButton)
            .Spacing(1)
            .HorizontalAlignment(Align.End);

        VStack fields = new VStack(
                new TextBlock("Name").Style(StraumrStyleService.MutedText),
                _nameField,
                new TextBlock("Location (optional)").Style(StraumrStyleService.MutedText),
                location)
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        VStack content = new VStack()
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);
        if (sourceName is not null)
        {
            content.Add(new HStack(
                    new TextBlock("Source").Style(StraumrStyleService.MutedText),
                    new TextBlock(sourceName).Style(StraumrStyleService.PrimaryText))
                .Spacing(2));
        }

        content.Add(fields);
        content.Add(actions);

        _dialog = StraumrDialogHelpers.Create(
            new TextBlock(title).Style(StraumrStyleService.AccentText),
            content,
            64);
        AddSubmitCommand(_nameInput, actionLabel);
        AddSubmitCommand(_locationInput, actionLabel);

        cancelButton.Click(() => _dialog.Close());
        actionButton.Click(TrySubmit);
    }

    private string? ResolvedLocation
    {
        get
        {
            string typed = _locationText.Value.Trim();
            return typed.Length > 0 ? typed : _defaultLocation;
        }
    }

    public void Show() =>
        TuiWindowHelpers.Show(_dialog, () => _nameInput.App?.Focus(_nameInput));

    private Visual BuildResolvedLocation() =>
        new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Cell(new TextBlock(ResolvedGlyph).Style(StraumrStyleService.MutedText), 0, 0)
            .Cell(
                new TextBlock(ResolvedLocationText)
                    .Style(() => ResolvedLocation is null
                        ? StraumrStyleService.AmberText
                        : StraumrStyleService.MutedBrightText)
                    .Trimming(() => ResolvedLocation is null
                        ? TextTrimming.EndEllipsis
                        : TextTrimming.StartEllipsis)
                    .HorizontalAlignment(Align.Stretch),
                0,
                1)
            .IsVisible(() => _locationText.Value.Trim().Length == 0)
            .HorizontalAlignment(Align.Stretch);

    private void SetLocation(string path)
    {
        _locationInput.SetText(path);
        _locationText.Value = path;
    }

    private string ResolvedLocationText() =>
        ResolvedLocation is { } location
            ? PathFormatting.Display(location)
            : "No default location";

    private void AddSubmitCommand(Visual target, string actionLabel) =>
        target.AddCommand(new Command
        {
            Id = "WorkspaceFormDialog.Submit",
            LabelMarkup = actionLabel,
            Gesture = TuiKeybindHelpers.Get("WorkspaceFormDialog.Submit"),
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

        _dialog.Close();
        _submit(new WorkspaceFormSubmissionModel(name, ResolvedLocation));
    }
}
