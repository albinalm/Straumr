using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared.Editor;

/// <summary>What a value in a key-value map is allowed to be.</summary>
internal enum KeyValueValueKind
{
    /// <summary>Text, and nothing else: a header, a query parameter, a form field.</summary>
    Text,

    /// <summary>
    /// Text or a file from disk, which is what a multipart form part is. A file part is stored as
    /// <c>@</c> followed by its path, the same encoding the CLI reads and writes.
    /// </summary>
    TextOrFile
}

/// <summary>
/// The modal a name/value pair is added or changed through. Headers, query parameters, form fields
/// and multipart parts are all this, differing only in what they call the two halves, in which names
/// they refuse, and in whether a value may be a file.
/// </summary>
internal sealed class KeyValuePairDialog
{
    private const int DialogWidth = 70;

    /// <summary>Leads a value that names a file rather than holding one.</summary>
    internal const char FileMarker = '@';

    private readonly Dialog _dialog;
    private readonly FormTextBox _key;
    private readonly FormTextBox _value;
    private readonly FormTextBox _path;
    private readonly ValidationPresenter _keyField;
    private readonly ValidationPresenter _pathField;

    /// <summary>The file picker, held because which of it and the value box shows is assigned.</summary>
    private readonly Visual _filePicker;
    private readonly State<bool> _isFile = new(false);
    private readonly Func<string, string?> _validateKey;
    private readonly Action<string, string> _submit;
    private readonly KeyValueValueKind _kind;

    /// <param name="validateKey">
    /// Returns the message to show, or <see langword="null"/> when the name is acceptable. It runs on
    /// submission rather than per keystroke, so a half-typed name does not read as an error.
    /// </param>
    public KeyValuePairDialog(
        string title,
        string keyLabel,
        string valueLabel,
        string initialKey,
        string initialValue,
        string confirmLabel,
        Func<string, string?> validateKey,
        Action<string, string> submit,
        KeyValueValueKind kind = KeyValueValueKind.Text)
    {
        _validateKey = validateKey;
        _submit = submit;
        _kind = kind;

        bool startsAsFile = kind == KeyValueValueKind.TextOrFile && initialValue.StartsWith(FileMarker);
        _isFile.Value = startsAsFile;

        _key = Field(keyLabel, startsAsFile ? initialKey : initialKey);
        _key.SetText(initialKey);
        _key.CaretIndex = initialKey.Length;
        _key.AutoFocus = true;
        _keyField = EditorVisuals.Validated(_key);
        _key.Changed = () => _keyField.Message = null;

        string text = startsAsFile ? string.Empty : initialValue;
        _value = Field(valueLabel.ToLowerInvariant(), text);

        string path = startsAsFile ? initialValue[1..] : string.Empty;
        _path = Field("path to the file to upload", path);
        _pathField = EditorVisuals.Validated(_path);
        _path.Changed = () => _pathField.Message = null;

        var content = new VStack(
                new TextBlock(keyLabel).Style(StraumrStyles.MutedText),
                _keyField)
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        if (kind == KeyValueValueKind.TextOrFile)
            content.Add(BuildKindPicker());

        content.Add(new TextBlock(() => _isFile.Value ? "File" : valueLabel).Style(StraumrStyles.MutedText));
        _filePicker = BuildFilePicker();
        content.Add(new ZStack(_value, _filePicker)
            .HorizontalAlignment(Align.Stretch));
        content.Add(new TextBlock(() => _isFile.Value
                ? "The file is read when the request is sent, not now, so it can change between sends."
                : "A value may reference a secret as {{secret:name}}.")
            .Style(StraumrStyles.MutedText)
            .Wrap(true));

        var cancelButton = new Button("Cancel");
        cancelButton.SetStyle(StraumrStyles.Button);

        var confirmButton = new Button(confirmLabel);
        confirmButton.SetStyle(StraumrStyles.PrimaryButton);
        confirmButton.Click(TrySubmit);

        content.Add(new HStack(cancelButton, confirmButton)
            .Spacing(1)
            .HorizontalAlignment(Align.End));

        _dialog = StraumrDialog.Create(
            new TextBlock(title).Style(StraumrStyles.AccentText),
            content,
            DialogWidth);

        cancelButton.Click(() => _dialog.Close());

        // Scoped to the fields rather than the dialog: a framework command runs before the focused
        // control sees the key, so a dialog-wide Enter would submit while a button had focus.
        AddSubmit(_key, confirmLabel);
        AddSubmit(_value, confirmLabel);
        AddSubmit(_path, confirmLabel);

        SyncKind();
    }

    /// <summary>
    /// Shows the half of the value the chosen kind uses and hides the other.
    /// </summary>
    /// <remarks>
    /// Assigned outright rather than bound, because both halves hold a text field. A focusable
    /// computes its own <c>IsTabStop</c> from its ancestors' visibility, and the framework refuses
    /// to let one update pass both read and write the same bindable value: a bound <c>IsVisible</c>
    /// over a subtree that reads it back is exactly that, and it threw on the first keystroke after
    /// the kind was chosen. It is also the same rule the editor's own pages follow, for the related
    /// reason that visibility has to be settled before the focus pass runs.
    /// </remarks>
    private void SyncKind()
    {
        _value.IsVisible = !_isFile.Value;
        _filePicker.IsVisible = _isFile.Value;
    }

    /// <summary>Arms the first field to discard the printable keystroke that opened this dialog.</summary>
    public char? PendingEcho
    {
        get => _key.PendingEcho;
        set => _key.PendingEcho = value;
    }

    public void Show() => _dialog.Show();

    private static FormTextBox Field(string placeholder, string initial)
    {
        FormTextBox box = FormTextBox.Create(placeholder);
        box.SetText(initial);
        box.CaretIndex = initial.Length;
        return box;
    }

    /// <remarks>
    /// A part is text or a file, never both, so the two value editors swap in one cell rather than
    /// sitting side by side: the one that does not apply is not something to leave on screen greyed.
    /// </remarks>
    private Visual BuildKindPicker()
    {
        var select = new Select<string>(["Text", "File"], _isFile.Value ? 1 : 0)
        {
            HorizontalAlignment = Align.Stretch
        };
        select.SetStyle(StraumrStyles.Select);
        select.WithMovementKeys();
        select.SelectionChanged(() =>
        {
            _isFile.Value = select.SelectedIndex == 1;
            SyncKind();
        });

        return new VStack(
                new TextBlock("Part").Style(StraumrStyles.MutedText),
                select)
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);
    }

    private Visual BuildFilePicker()
    {
        var browseButton = new Button("Browse");
        browseButton.SetStyle(StraumrStyles.Button);
        browseButton.Click(() => new FileBrowserDialog(
            Directory(_path.Text),
            null,
            path =>
            {
                _path.SetText(path);
                _path.CaretIndex = path.Length;
                _pathField.Message = null;
            },
            // Any file can be a multipart part, so the browser filters nothing and lists them all.
            _ => true,
            "Select file",
            "Attach file").Show());

        return new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star() },
                new ColumnDefinition { Width = GridLength.Auto })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(1)
            .Cell(_pathField, 0, 0)
            .Cell(browseButton, 0, 1)
            .HorizontalAlignment(Align.Stretch);
    }

    /// <summary>Where the browser should open: beside the file already chosen, if there is one.</summary>
    private static string? Directory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private void AddSubmit(Visual target, string confirmLabel) =>
        target.AddCommand(new Command
        {
            Id = "KeyValuePairDialog.Submit",
            LabelMarkup = confirmLabel,
            Gesture = new KeyGesture(TerminalKey.Enter),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => TrySubmit()
        });

    private void TrySubmit()
    {
        string key = (_key.Text ?? string.Empty).Trim();
        if (_validateKey(key) is { } message)
        {
            _keyField.Message = new ValidationMessage(ValidationSeverity.Error, new TextBlock(message));
            _key.App?.Focus(_key);
            return;
        }

        string value;
        if (_kind == KeyValueValueKind.TextOrFile && _isFile.Value)
        {
            string path = (_path.Text ?? string.Empty).Trim();
            // The path is checked here and not when the request is sent, because here is where
            // whoever typed it is still looking at it.
            if (path.Length == 0 || !File.Exists(path))
            {
                _pathField.Message = new ValidationMessage(ValidationSeverity.Error,
                    new TextBlock(path.Length == 0 ? "Choose a file." : "No file at that path."));
                _path.App?.Focus(_path);
                return;
            }

            value = FileMarker + path;
        }
        else
        {
            // Text is taken as typed. A header or a form field can legitimately carry leading or
            // trailing spaces, and the name is the half that has to be unambiguous.
            value = _value.Text ?? string.Empty;
        }

        _dialog.Close();
        _submit(key, value);
    }
}
