using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Straumr.Console.Tui.Components.ListViews;
using Straumr.Console.Tui.Components.Prompts.Base;
using Straumr.Console.Tui.Components.Text;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Factories;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.FileSave;

internal sealed class FileSavePrompt : PromptComponent
{
    public required string Title { get; init; }
    public string? InitialPath { get; init; }
    public bool MustExist { get; init; }
    public IReadOnlyList<IAllowedType>? AllowedTypes { get; init; }

    public event Action<string>? SaveRequested;
    public event Action? CancelRequested;

    private readonly ObservableCollection<MarkupLabel> _displayItems = [];
    private readonly List<FileBrowserEntry> _entries = [];
    private readonly List<FileBrowserEntry> _filteredEntries = [];
    private readonly List<FileTypeFilter> _filters = [];

    private SelectionListView? _listView;
    private Label? _emptyLabel;
    private Label? _pathLabel;
    private Label? _typeLabel;
    private Label? _fullPathLabel;
    private Label? _statusLabel;
    private Label? _filterLabel;
    private InteractiveTextField? _filterField;
    private InteractiveTextField? _fileNameField;

    private string _currentDirectory = Environment.CurrentDirectory;
    private string _fileName = string.Empty;
    private string _filterText = string.Empty;
    private int _activeFilterIndex;

    public override View Build()
    {
        ResolveInitialPath();
        InitializeFilters();

        FrameView frame = CreateFrame(Title);

        _pathLabel = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
        };

        _filterLabel = new Label
        {
            Text = "Filter:",
            X = 1,
            Y = 2,
            Visible = false,
        };

        _filterField = TextFieldFactory.CreateFilterField(OnFilterChanged, FocusList, FocusList);
        _filterField.X = Pos.Right(_filterLabel) + 1;
        _filterField.Y = _filterLabel.Y;
        _filterField.Width = Dim.Fill(3);
        _filterField.Visible = false;
        ApplyFieldTheme(_filterField);

        _listView = new SelectionListView(HandleListKeyDown, BuildListScheme())
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Height = Dim.Fill(6),
        };
        _listView.SetMarkupSource(_displayItems);

        _emptyLabel = new Label
        {
            Text = "No matches",
            X = _listView.X,
            Y = _listView.Y,
            Width = _listView.Width,
            Visible = false,
        };

        Label fileNameLabel = new()
        {
            Text = "File name:",
            X = 1,
            Y = Pos.AnchorEnd(5),
        };

        _fileNameField = TextFieldFactory.CreatePromptField(OnFileNameChanged, TryAcceptSelection, RequestCancel);
        _fileNameField.X = Pos.Right(fileNameLabel) + 1;
        _fileNameField.Y = fileNameLabel.Y;
        _fileNameField.Width = Dim.Fill(2);
        ApplyFieldTheme(_fileNameField);
        _fileNameField.Bind(Key.Tab, (_, _) =>
        {
            FocusList();
            return true;
        });

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
            Text = string.Empty,
        };

        _statusLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(2),
            Visible = false,
        };

        frame.Add(_pathLabel);
        frame.Add(_filterLabel, _filterField);
        frame.Add(_listView, _emptyLabel);
        frame.Add(fileNameLabel, _fileNameField);
        frame.Add(_typeLabel, _fullPathLabel, _statusLabel);

        _listView.Initialized += (_, _) => _listView.SetFocus();

        UpdatePathLabel();
        RefreshFilterRowLayout();
        RefreshTypeLabel();
        LoadDirectory(_currentDirectory);
        SetFileName(_fileName);
        UpdateFullPathLabel();

        return frame;
    }

    private void ResolveInitialPath()
    {
        if (string.IsNullOrWhiteSpace(InitialPath))
        {
            _currentDirectory = TryResolveDirectory(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
            _fileName = string.Empty;
            return;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(InitialPath);
        }
        catch
        {
            _currentDirectory = TryResolveDirectory(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
            _fileName = string.Empty;
            return;
        }

        if (Directory.Exists(candidate))
        {
            _currentDirectory = candidate;
            _fileName = string.Empty;
            return;
        }

        string? directory = Path.GetDirectoryName(candidate);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            _currentDirectory = directory;
        }
        else
        {
            _currentDirectory = TryResolveDirectory(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
        }

        _fileName = Path.GetFileName(candidate);
    }

    private string? TryResolveDirectory(string value)
    {
        try
        {
            string resolved = Path.GetFullPath(value);
            return Directory.Exists(resolved) ? resolved : null;
        }
        catch
        {
            return null;
        }
    }

    private void InitializeFilters()
    {
        if (AllowedTypes is not null)
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
    }

    private void ApplyFieldTheme(InteractiveTextField? field)
    {
        if (field is null || Theme is null)
        {
            return;
        }

        Color background = ColorResolver.Resolve(Theme.Surface);
        Color foreground = ColorResolver.Resolve(Theme.OnSurface);
        field.ApplyTheme(background, foreground);
    }

    private void LoadDirectory(string directory)
    {
        string target;
        try
        {
            target = Path.GetFullPath(directory);
        }
        catch (Exception ex)
        {
            ShowStatus($"Invalid directory: {ex.Message}");
            return;
        }

        if (!Directory.Exists(target))
        {
            ShowStatus("Directory does not exist.");
            return;
        }

        _currentDirectory = target;
        UpdatePathLabel();

        List<FileBrowserEntry> buffer = [];

        try
        {
            DirectoryInfo info = new(target);
            if (info.Parent is not null)
            {
                buffer.Add(FileBrowserEntry.Parent(info.Parent.FullName));
            }

            foreach (DirectoryInfo dir in info.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                buffer.Add(FileBrowserEntry.Directory(dir));
            }

            foreach (FileInfo file in info.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                buffer.Add(FileBrowserEntry.File(file));
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Unable to read directory: {ex.Message}");
            return;
        }

        _entries.Clear();
        _entries.AddRange(buffer);
        ApplyEntryFilter();
    }

    private void ApplyEntryFilter()
    {
        _filteredEntries.Clear();
        _listView?.BeginBulkUpdate();
        _displayItems.Clear();

        FileTypeFilter currentFilter = _filters[_activeFilterIndex];

        foreach (FileBrowserEntry entry in _entries)
        {
            if (entry.Kind == FileEntryKind.File && !currentFilter.Matches(entry.FullPath))
            {
                continue;
            }

            if (!MatchesFilter(entry))
            {
                continue;
            }

            _filteredEntries.Add(entry);
            _displayItems.Add(new MarkupLabel
            {
                Markup = BuildMarkup(entry),
                Theme = Theme,
            });
        }

        _listView?.EndBulkUpdate();

        if (_listView is not null)
        {
            _listView.SelectedItem = _filteredEntries.Count > 0 ? 0 : null;
            _listView.Visible = _filteredEntries.Count > 0;
        }

        if (_emptyLabel is not null)
        {
            _emptyLabel.Visible = _filteredEntries.Count == 0;
        }
    }

    private bool MatchesFilter(FileBrowserEntry entry)
    {
        if (entry.Kind == FileEntryKind.Parent)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_filterText))
        {
            return true;
        }

        return entry.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildMarkup(FileBrowserEntry entry)
    {
        string glyph = entry.Kind switch
        {
            FileEntryKind.Directory => "📁",
            FileEntryKind.Parent => "↩",
            _ => "📄"
        };

        string nameColor = entry.Kind switch
        {
            FileEntryKind.Directory => "[accent]",
            FileEntryKind.Parent => "[secondary]",
            _ => "[primary]"
        };

        string name = $"{nameColor}{entry.Name}[/]";
        string description = entry.Kind switch
        {
            FileEntryKind.Directory => "[secondary]Directory[/]",
            FileEntryKind.Parent => "[secondary]Parent directory[/]",
            _ => $"[secondary]{FormatSize(entry.Size)}  •  {entry.Extension.ToUpperInvariant()}[/]"
        };

        string modified = entry.Kind == FileEntryKind.Parent
            ? string.Empty
            : entry.ModifiedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;

        if (!string.IsNullOrEmpty(modified))
        {
            modified = $"[secondary]Modified {modified}[/]";
        }

        return $"◇ {glyph} {name}\n    {description}\n    {modified}";
    }

    private static string FormatSize(long? size)
    {
        if (!size.HasValue || size.Value < 0)
        {
            return "Unknown size";
        }

        double bytes = size.Value;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int unitIndex = 0;

        while (bytes >= 1024 && unitIndex < units.Length - 1)
        {
            bytes /= 1024;
            unitIndex++;
        }

        return $"{bytes:0.##} {units[unitIndex]}";
    }

    private bool HandleListKeyDown(Key key)
    {
        if (_listView is null)
        {
            return false;
        }

        if (!key.IsCtrl && !key.IsAlt)
        {
            switch (KeyHelpers.GetCharValue(key))
            {
                case 'j':
                    MoveSelection(1);
                    return true;
                case 'k':
                    MoveSelection(-1);
                    return true;
                case 'g':
                    MoveSelectionTo(0);
                    return true;
                case 'G':
                    MoveSelectionTo(_filteredEntries.Count - 1);
                    return true;
                case 'h':
                    NavigateUp();
                    return true;
                case 'l':
                    ActivateSelection();
                    return true;
                case '/':
                    FocusFilter();
                    return true;
                case 't':
                    CycleFilter(1);
                    return true;
            }
        }

        if (key == Key.Enter)
        {
            ActivateSelection();
            return true;
        }

        if (key == Key.Tab)
        {
            FocusFileName();
            return true;
        }

        if (key == Key.Backspace)
        {
            NavigateUp();
            return true;
        }

        if (key == Key.Esc)
        {
            CancelRequested?.Invoke();
            return true;
        }

        return false;
    }

    private void MoveSelection(int delta)
    {
        if (_listView is null || _filteredEntries.Count == 0)
        {
            return;
        }

        int rowsPerItem = MarkupLabelListDataSource.RowsPerItem;
        int current = _listView.SelectedItem ?? 0;
        int logical = current / rowsPerItem;
        int next = Math.Clamp(logical + delta, 0, _filteredEntries.Count - 1);
        _listView.SelectedItem = next * rowsPerItem;
    }

    private void MoveSelectionTo(int target)
    {
        if (_listView is null || _filteredEntries.Count == 0)
        {
            return;
        }

        int next = Math.Clamp(target, 0, _filteredEntries.Count - 1);
        _listView.SelectedItem = next * MarkupLabelListDataSource.RowsPerItem;
    }

    private void ActivateSelection()
    {
        if (_listView is null)
        {
            return;
        }

        int? selectedRow = _listView.SelectedItem;
        if (selectedRow is null)
        {
            return;
        }

        int logicalIndex = selectedRow.Value / MarkupLabelListDataSource.RowsPerItem;
        if (logicalIndex < 0 || logicalIndex >= _filteredEntries.Count)
        {
            return;
        }

        FileBrowserEntry entry = _filteredEntries[logicalIndex];
        switch (entry.Kind)
        {
            case FileEntryKind.Directory:
                LoadDirectory(entry.FullPath);
                break;
            case FileEntryKind.Parent:
                NavigateUp();
                break;
            case FileEntryKind.File:
                SetFileName(entry.Name);
                TryAcceptSelection();
                break;
        }
    }

    private void NavigateUp()
    {
        DirectoryInfo current = new(_currentDirectory);
        if (current.Parent is null)
        {
            return;
        }

        LoadDirectory(current.Parent.FullName);
    }

    private void FocusFilter()
    {
        if (_filterField is null)
        {
            return;
        }

        RefreshFilterRowLayout(forceVisible: true);
        _filterField.SetFocus();
        _filterField.EnterEditMode();
    }

    private void FocusList()
    {
        _listView?.SetFocus();
        RefreshFilterRowLayout();
    }

    private void FocusFileName()
    {
        if (_fileNameField is null)
        {
            return;
        }

        _fileNameField.SetFocus();
        _fileNameField.EnterEditMode();
    }

    private void RefreshFilterRowLayout(bool forceVisible = false)
    {
        if (_filterLabel is null || _filterField is null || _listView is null || _emptyLabel is null)
        {
            return;
        }

        bool hasText = !string.IsNullOrEmpty(_filterField.Text?.ToString());
        bool visible = forceVisible || hasText || _filterField.HasFocus;

        _filterLabel.Visible = visible;
        _filterField.Visible = visible;

        int listOffset = visible ? 4 : 2;
        _listView.Y = listOffset;
        _emptyLabel.Y = listOffset;
        _listView.Height = visible ? Dim.Fill(7) : Dim.Fill(6);
    }

    private void OnFilterChanged(string text)
    {
        _filterText = text;
        ApplyEntryFilter();
        RefreshFilterRowLayout();
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

    private void RefreshTypeLabel()
    {
        FileTypeFilter filter = _filters[_activeFilterIndex];
        if (_typeLabel is not null)
        {
            _typeLabel.Text = $"Type: {filter.Label}";
        }
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

    private bool TryAcceptSelection()
    {
        if (_fileNameField is null)
        {
            return true;
        }

        string candidate = _fileNameField.Text ?? string.Empty;
        candidate = candidate.Trim();

        if (candidate.Length == 0)
        {
            ShowStatus("Enter a file name.");
            return true;
        }

        if (!TryResolveFullPath(candidate, out string fullPath, out string? error))
        {
            ShowStatus(error ?? "Unable to resolve path.");
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

        SaveRequested?.Invoke(fullPath);
        return true;
    }

    private bool TryResolveFullPath(string input, out string fullPath, out string? error)
    {
        string combined;
        if (Path.IsPathRooted(input))
        {
            combined = input;
        }
        else
        {
            combined = Path.Combine(_currentDirectory, input);
        }

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

    private bool RequestCancel()
    {
        CancelRequested?.Invoke();
        return true;
    }

    private void OnFileNameChanged()
    {
        _fileName = _fileNameField?.Text ?? string.Empty;
        HideStatus();
        UpdateFullPathLabel();
    }

    private void UpdatePathLabel()
    {
        if (_pathLabel is not null)
        {
            _pathLabel.Text = $"Directory: {_currentDirectory}";
        }
    }

    private void UpdateFullPathLabel()
    {
        string display = _fileName.Length == 0
            ? _currentDirectory
            : Path.Combine(_currentDirectory, _fileName);

        if (_fullPathLabel is not null)
        {
            _fullPathLabel.Text = $"Full path: {display}";
        }
    }

    private void ShowStatus(string message)
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Text = message;
        _statusLabel.Visible = true;
    }

    private void HideStatus()
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Visible = false;
    }

    private sealed record FileBrowserEntry(
        string Name,
        string FullPath,
        FileEntryKind Kind,
        long? Size,
        DateTime? ModifiedUtc)
    {
        public static FileBrowserEntry Directory(DirectoryInfo info)
            => new(info.Name, info.FullName, FileEntryKind.Directory, null, info.LastWriteTimeUtc);

        public static FileBrowserEntry File(FileInfo info)
            => new(info.Name, info.FullName, FileEntryKind.File, info.Length, info.LastWriteTimeUtc);

        public static FileBrowserEntry Parent(string fullPath)
            => new("..", fullPath, FileEntryKind.Parent, null, null);

        public string Extension => Kind == FileEntryKind.File ? Path.GetExtension(Name) : string.Empty;
    }

    private enum FileEntryKind
    {
        Parent,
        Directory,
        File
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
