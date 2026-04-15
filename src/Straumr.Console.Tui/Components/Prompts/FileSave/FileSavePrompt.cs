using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Enums;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.FileSave;

internal sealed class FileSavePrompt : FileSystemPromptBase
{
    public bool MustExist { get; init; }
    public IReadOnlyList<IAllowedType>? AllowedTypes { get; init; }

    public event Action<string>? SaveRequested;

    private readonly List<FileTypeFilter> _filters = [];
    private int _activeFilterIndex;
    private InteractiveTextField? _fileNameField;
    private Label? _typeLabel;
    private Label? _fullPathLabel;
    private string _fileName = string.Empty;

    protected override void InitializeStartingDirectory()
    {
        ResetToHomeDirectory();

        if (string.IsNullOrWhiteSpace(InitialPath))
        {
            _fileName = string.Empty;
            return;
        }

        string candidate;
        try
        {
            candidate = Path.IsPathRooted(InitialPath)
                ? Path.GetFullPath(InitialPath)
                : Path.GetFullPath(InitialPath, CurrentDirectory);
        }
        catch
        {
            _fileName = Path.GetFileName(InitialPath);
            return;
        }

        if (Directory.Exists(candidate))
        {
            SetCurrentDirectory(candidate);
            _fileName = string.Empty;
            return;
        }

        string? directory = Path.GetDirectoryName(candidate);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            SetCurrentDirectory(directory);
        }

        string name = Path.GetFileName(candidate);
        _fileName = string.IsNullOrWhiteSpace(name) ? string.Empty : name;
    }

    protected override void BuildContent(FrameView frame)
    {
        InitializeFilters();

        Label fileNameLabel = new()
        {
            Text = "File name:",
            X = 1,
            Y = Pos.AnchorEnd(5),
        };

        _fileNameField = new InteractiveTextField
        {
            X = Pos.Right(fileNameLabel) + 1,
            Y = fileNameLabel.Y,
            Width = Dim.Fill(2),
            Text = _fileName,
        };

        _fileNameField.TextChanged += (_, _) => OnFileNameChanged();
        _fileNameField.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsEnter(key), (_, _) =>
        {
            FocusList();
            return true;
        }));
        _fileNameField.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsTabForward(key), (_, _) =>
        {
            FocusList();
            return true;
        }));
        _fileNameField.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsEscape(key), (_, _) =>
        {
            FocusList();
            return true;
        }));
        ApplyFieldTheme(_fileNameField);

        _typeLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(4),
            Width = Dim.Fill(2),
        };

        _fullPathLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(2),
        };

        frame.Add(fileNameLabel, _fileNameField, _typeLabel, _fullPathLabel);

        SetFileName(_fileName);
        RefreshTypeLabel();
        UpdateFullPathLabel();
    }

    protected override void OnAfterDirectoryChanged()
    {
        UpdateFullPathLabel();
    }

    protected override bool HandleCustomKey(Key key)
    {
        int ch = KeyHelpers.GetCharValue(key);
        if (ch is 't' or 'T')
        {
            CycleFilter(1);
            return true;
        }

        if (ch is 's' or 'S')
        {
            return TryAcceptSelection();
        }

        return false;
    }

    protected override void OnTabPressed()
    {
        _fileNameField?.SetFocus();
        _fileNameField?.EnterEditMode();
    }

    protected override void OnFileEntryActivated(FileBrowserEntry entry)
    {
        SetFileName(entry.Name);
        TryAcceptSelection();
    }

    protected override bool ShouldIncludeEntry(FileBrowserEntry entry)
    {
        if (entry.Kind != FileEntryKind.File)
        {
            return true;
        }

        if (_filters.Count == 0)
        {
            return true;
        }

        FileTypeFilter filter = _filters[_activeFilterIndex];
        return filter.Matches(entry.FullPath);
    }

    private void InitializeFilters()
    {
        _filters.Clear();

        if (AllowedTypes is { Count: > 0 })
        {
            foreach (IAllowedType allowed in AllowedTypes)
            {
                _filters.Add(FileTypeFilter.FromAllowedType(allowed));
            }
        }

        if (_filters.Count == 0)
        {
            _filters.Add(FileTypeFilter.AllowAll());
        }

        _activeFilterIndex = Math.Clamp(_activeFilterIndex, 0, _filters.Count - 1);
    }

    private void OnFileNameChanged()
    {
        _fileName = _fileNameField?.Text ?? string.Empty;
        HideStatus();
        UpdateFullPathLabel();
    }

    private void SetFileName(string? value, bool updateField = true)
    {
        _fileName = value ?? string.Empty;
        if (updateField && _fileNameField is not null)
        {
            _fileNameField.Text = _fileName;
        }

        UpdateFullPathLabel();
    }

    private void RefreshTypeLabel()
    {
        if (_typeLabel is null || _filters.Count == 0)
        {
            return;
        }

        FileTypeFilter filter = _filters[_activeFilterIndex];
        _typeLabel.Text = $"Type: {filter.Label}";
    }

    private void UpdateFullPathLabel()
    {
        if (_fullPathLabel is null)
        {
            return;
        }

        string fileName = _fileNameField?.Text ?? _fileName;
        string display = string.IsNullOrWhiteSpace(fileName)
            ? CurrentDirectory
            : Path.Combine(CurrentDirectory, fileName);

        _fullPathLabel.Text = $"Full path: {display}";
    }

    private void CycleFilter(int delta)
    {
        if (_filters.Count <= 1)
        {
            return;
        }

        _activeFilterIndex = (_activeFilterIndex + delta + _filters.Count) % _filters.Count;
        RefreshTypeLabel();
        ApplyEntryFilter();
    }

    private bool TryAcceptSelection()
    {
        if (_fileNameField is null)
        {
            return true;
        }

        string candidate = _fileNameField.Text.Trim();
        if (candidate.Length == 0)
        {
            ShowStatus("Enter a file name.");
            return true;
        }

        if (!TryResolveFullPath(candidate, out string fullPath, out string? error))
        {
            ShowStatus(error ?? "Invalid path.");
            return true;
        }

        if (MustExist && !File.Exists(fullPath))
        {
            ShowStatus("Selected file does not exist.");
            return true;
        }

        FileTypeFilter filter = _filters[_activeFilterIndex];
        if (!filter.Matches(fullPath))
        {
            ShowStatus($"File must match {filter.Label}.");
            return true;
        }

        HideStatus();
        SaveRequested?.Invoke(fullPath);
        return true;
    }

    private bool TryResolveFullPath(string input, out string fullPath, out string? error)
    {
        string combined = Path.IsPathRooted(input)
            ? input
            : Path.Combine(CurrentDirectory, input);

        try
        {
            fullPath = Path.GetFullPath(combined);
        }
        catch (Exception ex)
        {
            fullPath = string.Empty;
            error = ex.Message;
            return false;
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            error = "Target directory does not exist.";
            return false;
        }

        error = null;
        return true;
    }

    private sealed record FileTypeFilter(string Label, Func<string, bool> Predicate)
    {
        public static FileTypeFilter FromAllowedType(IAllowedType allowed)
        {
            if (allowed is AllowedTypeAny)
            {
                return AllowAll();
            }

            if (allowed is AllowedType typed)
            {
                string extensions = typed.Extensions is { Length: > 0 }
                    ? string.Join(", ", typed.Extensions)
                    : "*.*";
                return new FileTypeFilter($"{typed.Description} ({extensions})", allowed.IsAllowed);
            }

            string description = allowed.ToString() ?? "All files";
            return new FileTypeFilter(description, allowed.IsAllowed);
        }

        public static FileTypeFilter AllowAll()
            => new("All files (*.*)", _ => true);

        public bool Matches(string path) => Predicate(path);
    }
}
