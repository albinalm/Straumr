using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class KeyValuePairDialog
{
    private const int DialogWidth = 70;

    internal const char FileMarker = '@';

    private readonly Dialog _dialog;

    private readonly Visual _filePicker;
    private readonly State<bool> _isFile = new(false);
    private readonly FormTextBox _key;
    private readonly ValidationPresenter _keyField;
    private readonly KeyValueValueKind _kind;
    private readonly FormTextBox _path;
    private readonly ValidationPresenter _pathField;
    private readonly Action<string, string> _submit;
    private readonly Func<string, string?> _validateKey;
    private readonly FormTextBox _value;

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
        _keyField = EditorVisualHelpers.Validated(_key.Root);
        _key.Changed = () => _keyField.Message = null;

        string text = startsAsFile ? string.Empty : initialValue;
        _value = Field(valueLabel.ToLowerInvariant(), text);

        string path = startsAsFile ? initialValue[1..] : string.Empty;
        _path = Field("path to the file to upload", path);
        _pathField = EditorVisualHelpers.Validated(_path.Root);
        _path.Changed = () => _pathField.Message = null;

        VStack content = new VStack(
                new TextBlock(keyLabel).Style(StraumrStyleService.MutedText),
                _keyField)
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        if (kind == KeyValueValueKind.TextOrFile)
        {
            content.Add(BuildKindPicker());
        }

        content.Add(new TextBlock(() => _isFile.Value ? "File" : valueLabel).Style(StraumrStyleService.MutedText));
        _filePicker = BuildFilePicker();
        content.Add(new ZStack(_value.Root, _filePicker)
            .HorizontalAlignment(Align.Stretch));
        content.Add(new TextBlock(() => _isFile.Value
                ? "The file is read when the request is sent, not now, so it can change between sends."
                : "A value may reference a secret as {{secret:name}}.")
            .Style(StraumrStyleService.MutedText)
            .Wrap(true));

        var cancelButton = new Button("Cancel");
        cancelButton.SetStyle(StraumrStyleService.Button);

        var confirmButton = new Button(confirmLabel);
        confirmButton.SetStyle(StraumrStyleService.PrimaryButton);
        confirmButton.Click(TrySubmit);

        content.Add(new HStack(cancelButton, confirmButton)
            .Spacing(1)
            .HorizontalAlignment(Align.End));

        _dialog = StraumrDialogHelpers.Create(
            new TextBlock(title).Style(StraumrStyleService.AccentText),
            content,
            DialogWidth);

        cancelButton.Click(() => _dialog.Close());

        AddSubmit(_key, confirmLabel);
        AddSubmit(_value, confirmLabel);
        AddSubmit(_path, confirmLabel);

        SyncKind();
    }

    public char? PendingEcho
    {
        get => _key.PendingEcho;
        set => _key.PendingEcho = value;
    }

    private void SyncKind()
    {
        _value.Root.IsVisible = !_isFile.Value;
        _filePicker.IsVisible = _isFile.Value;
    }

    public void Show() => TuiWindowHelpers.Show(_dialog);

    private static FormTextBox Field(string placeholder, string initial)
    {
        var box = FormTextBox.Create(placeholder);
        box.SetText(initial);
        box.CaretIndex = initial.Length;
        return box;
    }

    private Visual BuildKindPicker()
    {
        Select<string> select = new(["Text", "File"], _isFile.Value ? 1 : 0)
        {
            HorizontalAlignment = Align.Stretch
        };
        select.SetStyle(StraumrStyleService.Select);
        select.WithMovementKeys();
        select.SelectionChanged(() =>
        {
            _isFile.Value = select.SelectedIndex == 1;
            SyncKind();
        });

        return new VStack(
                new TextBlock("Part").Style(StraumrStyleService.MutedText),
                select)
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);
    }

    private Visual BuildFilePicker()
    {
        var browseButton = new Button("Browse");
        browseButton.SetStyle(StraumrStyleService.Button);
        browseButton.Click(() => new FileBrowserDialog(
            Directory(_path.Text),
            null,
            path =>
            {
                _path.SetText(path);
                _path.CaretIndex = path.Length;
                _pathField.Message = null;
            },
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

    private static string? Directory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

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
            Gesture = TuiKeybindHelpers.Get("KeyValuePairDialog.Submit"),
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
            value = _value.Text ?? string.Empty;
        }

        _dialog.Close();
        _submit(key, value);
    }
}
