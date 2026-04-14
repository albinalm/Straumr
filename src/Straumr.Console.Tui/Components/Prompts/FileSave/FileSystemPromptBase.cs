using System;
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
using Straumr.Core;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.FileSave;

internal abstract class FileSystemPromptBase : PromptComponent
{
    private readonly ObservableCollection<MarkupLabel> _displayItems = [];
    private readonly List<FileBrowserEntry> _entries = [];
    private readonly List<FileBrowserEntry> _filteredEntries = [];

    private SelectionListView? _listView;
    private Label? _emptyLabel;
    private Label? _pathLabel;
    private Label? _statusLabel;
    private Label? _filterLabel;
    private InteractiveTextField? _filterField;
    private Label? _goToLabel;
    private InteractiveTextField? _goToField;
    private Label? _newDirLabel;
    private InteractiveTextField? _newDirField;
    private FileBrowserEntry? _pendingDelete;

    private readonly string _homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private string _currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private string _filterText = string.Empty;

    public required string Title { get; init; }
    public string? InitialPath { get; init; }

    public event Action? CancelRequested;

    protected SelectionListView? ListView => _listView;
    protected string CurrentDirectory => _currentDirectory;
    protected IReadOnlyList<FileBrowserEntry> FilteredEntries => _filteredEntries;

    protected virtual int FooterReservedRows => 6;

    public override View Build()
    {
        InitializeStartingDirectory();

        FrameView frame = CreateFrame(Title);
        BuildBaseLayout(frame);
        BuildContent(frame);

        _listView!.Initialized += (_, _) => _listView.SetFocus();
        UpdatePathLabel();
        RefreshLayout();
        LoadDirectory(_currentDirectory);

        return frame;
    }

    protected abstract void BuildContent(FrameView frame);

    protected virtual void OnAfterDirectoryChanged()
    {
    }

    protected virtual bool HandleCustomKey(Key key) => false;

    protected virtual void OnFileEntryActivated(FileBrowserEntry entry)
    {
    }

    protected virtual void OnDirectoryEntryActivated(FileBrowserEntry entry)
    {
        LoadDirectory(entry.FullPath);
    }

    protected virtual void OnParentEntryActivated(FileBrowserEntry entry)
    {
        DirectoryInfo current = new(_currentDirectory);
        if (current.Parent is null)
        {
            return;
        }

        LoadDirectory(current.Parent.FullName);
    }

    protected virtual bool ShouldIncludeEntry(FileBrowserEntry entry) => true;

    protected void FocusList()
    {
        _listView?.SetFocus();
        RefreshLayout();
    }

    protected void SetCurrentDirectory(string directory)
    {
        _currentDirectory = directory;
        UpdatePathLabel();
    }

    protected IReadOnlyList<FileBrowserEntry> GetFilteredEntries() => _filteredEntries;

    protected void ShowStatus(string message)
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Text = message;
        _statusLabel.Visible = true;
    }

    protected void HideStatus()
    {
        if (_statusLabel is null)
        {
            return;
        }

        _statusLabel.Visible = false;
    }

    protected virtual void InitializeStartingDirectory()
    {
        if (string.IsNullOrWhiteSpace(_homeDirectory))
        {
            _currentDirectory = Environment.CurrentDirectory;
            return;
        }

        _currentDirectory = TryResolveDirectory(_homeDirectory) ?? Environment.CurrentDirectory;
    }

    protected void ResetToHomeDirectory()
    {
        SetCurrentDirectory(TryResolveDirectory(_homeDirectory) ?? Environment.CurrentDirectory);
    }

    protected void LoadDirectory(string directory)
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
        HideStatus();

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
        OnAfterDirectoryChanged();
    }

    private void BuildBaseLayout(FrameView frame)
    {
        _pathLabel = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
        };

        _goToLabel = new Label
        {
            Text = "Go to:",
            X = 1,
            Y = 2,
            Visible = false,
        };

        _goToField = CreateGoToField();
        _goToField.X = Pos.Right(_goToLabel) + 1;
        _goToField.Y = _goToLabel.Y;
        _goToField.Width = Dim.Fill(3);
        _goToField.Visible = false;
        ApplyFieldTheme(_goToField);

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

        _newDirLabel = new Label
        {
            Text = "New dir:",
            X = 1,
            Y = 2,
            Visible = false,
        };

        _newDirField = CreateNewDirField();
        _newDirField.X = Pos.Right(_newDirLabel) + 1;
        _newDirField.Y = _newDirLabel.Y;
        _newDirField.Width = Dim.Fill(3);
        _newDirField.Visible = false;
        ApplyFieldTheme(_newDirField);

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

        _statusLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(2),
            Visible = false,
        };

        frame.Add(_pathLabel);
        frame.Add(_goToLabel, _goToField);
        frame.Add(_filterLabel, _filterField);
        frame.Add(_newDirLabel, _newDirField);
        frame.Add(_listView, _emptyLabel, _statusLabel);
    }

    protected void ApplyEntryFilter()
    {
        _filteredEntries.Clear();
        _listView?.BeginBulkUpdate();
        _displayItems.Clear();

        foreach (FileBrowserEntry entry in _entries)
        {
            if (!ShouldIncludeEntry(entry))
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

    private bool HandleListKeyDown(Key key)
    {
        if (_listView is null)
        {
            return false;
        }

        if (_pendingDelete is not null && !key.IsCtrl && !key.IsAlt)
        {
            int confirmChar = KeyHelpers.GetCharValue(key);
            if (confirmChar is 'y' or 'Y')
            {
                ExecutePendingDelete();
                return true;
            }

            _pendingDelete = null;
            HideStatus();
            return true;
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
                case 'o':
                case 'O':
                    ActivateSelection();
                    return true;
                case '/':
                    FocusFilter();
                    return true;
                case 'p':
                case 'P':
                    BeginGoTo();
                    return true;
                case 'n':
                case 'N':
                    BeginNewDirectory();
                    return true;
                case 'D':
                    BeginDeleteDirectory();
                    return true;
            }
        }

        if (HandleCustomKey(key))
        {
            return true;
        }

        if (key == Key.Enter)
        {
            ActivateSelection();
            return true;
        }

        if (key == Key.Tab && (_goToField is null || !_goToField.Visible))
        {
            OnTabPressed();
            return true;
        }

        if (key == Key.Backspace)
        {
            NavigateUp();
            return true;
        }

        if (key == Key.Esc)
        {
            RequestCancel();
            return true;
        }

        return false;
    }

    protected virtual void OnTabPressed()
    {
        FocusList();
    }

    protected void RequestCancel() => CancelRequested?.Invoke();

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

    protected FileBrowserEntry? GetSelectedEntry()
    {
        if (_listView is null)
        {
            return null;
        }

        int? selectedRow = _listView.SelectedItem;
        if (selectedRow is null)
        {
            return null;
        }

        int logicalIndex = selectedRow.Value / MarkupLabelListDataSource.RowsPerItem;
        if (logicalIndex < 0 || logicalIndex >= _filteredEntries.Count)
        {
            return null;
        }

        return _filteredEntries[logicalIndex];
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
                OnDirectoryEntryActivated(entry);
                break;
            case FileEntryKind.Parent:
                OnParentEntryActivated(entry);
                break;
            case FileEntryKind.File:
                OnFileEntryActivated(entry);
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

        RefreshLayout(forceFilterVisible: true);
        _filterField.SetFocus();
        _filterField.EnterEditMode();
    }

    private void RefreshLayout(bool forceFilterVisible = false)
    {
        if (_listView is null || _emptyLabel is null)
        {
            return;
        }

        int nextRow = 2;

        if (_goToLabel is not null && _goToField is not null)
        {
            bool goToVisible = _goToField.Visible;
            _goToLabel.Visible = goToVisible;
            if (goToVisible)
            {
                _goToLabel.Y = nextRow;
                _goToField.Y = nextRow;
                nextRow++;
            }
        }

        bool showFilter = _filterLabel is not null && _filterField is not null
                          && (forceFilterVisible
                              || _filterField.HasFocus
                              || !string.IsNullOrEmpty(_filterField.Text?.ToString()));

        if (_filterLabel is not null)
        {
            _filterLabel.Visible = showFilter;
        }

        if (_filterField is not null)
        {
            _filterField.Visible = showFilter;
        }

        if (showFilter && _filterLabel is not null && _filterField is not null)
        {
            _filterLabel.Y = nextRow;
            _filterField.Y = nextRow;
            nextRow++;
        }

        if (_newDirLabel is not null && _newDirField is not null && _newDirField.Visible)
        {
            _newDirLabel.Y = nextRow;
            _newDirField.Y = nextRow;
            nextRow++;
        }

        _listView.Y = nextRow;
        _emptyLabel.Y = nextRow;

        int reserved = FooterReservedRows;
        if (_goToField is not null && _goToField.Visible)
        {
            reserved++;
        }

        if (showFilter)
        {
            reserved++;
        }

        if (_newDirField is not null && _newDirField.Visible)
        {
            reserved++;
        }

        _listView.Height = Dim.Fill(reserved);
    }

    private void OnFilterChanged(string text)
    {
        _filterText = text;
        ApplyEntryFilter();
        RefreshLayout();
    }

    protected void UpdatePathLabel()
    {
        if (_pathLabel is not null)
        {
            _pathLabel.Text = $"Directory: {_currentDirectory}";
        }
    }

    private string? TryResolveDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

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

    private InteractiveTextField CreateNewDirField()
    {
        var field = new InteractiveTextField();
        field.Bind(Key.Enter, (_, _) => TryCreateNewDirectory());
        field.Bind(Key.Esc, (_, _) =>
        {
            HideNewDir();
            return true;
        });
        return field;
    }

    private void BeginNewDirectory()
    {
        if (_newDirField is null)
        {
            return;
        }

        _pendingDelete = null;
        HideStatus();
        _newDirField.Visible = true;
        _newDirField.Text = string.Empty;
        _newDirField.SetFocus();
        _newDirField.EnterEditMode();
        RefreshLayout();
    }

    private void HideNewDir()
    {
        if (_newDirField is null)
        {
            return;
        }

        _newDirField.Visible = false;
        RefreshLayout();
        FocusList();
    }

    private bool TryCreateNewDirectory()
    {
        if (_newDirField is null)
        {
            return true;
        }

        string name = (_newDirField.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            HideNewDir();
            return true;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ShowStatus("Invalid directory name.");
            return true;
        }

        string target = Path.Combine(CurrentDirectory, name);
        try
        {
            Directory.CreateDirectory(target);
        }
        catch (Exception ex)
        {
            ShowStatus($"Unable to create directory: {ex.Message}");
            return true;
        }

        HideNewDir();
        LoadDirectory(CurrentDirectory);
        return true;
    }

    private void BeginDeleteDirectory()
    {
        FileBrowserEntry? entry = GetSelectedEntry();
        if (entry is null || entry.Kind != FileEntryKind.Directory)
        {
            ShowStatus("Select a directory to delete.");
            return;
        }

        _pendingDelete = entry;
        ShowStatus($"Delete '{entry.Name}'? Press y to confirm, any other key to cancel.");
    }

    private void ExecutePendingDelete()
    {
        FileBrowserEntry? entry = _pendingDelete;
        _pendingDelete = null;
        if (entry is null)
        {
            HideStatus();
            return;
        }

        try
        {
            Directory.Delete(entry.FullPath, recursive: false);
        }
        catch (IOException)
        {
            ShowStatus("Directory is not empty.");
            return;
        }
        catch (Exception ex)
        {
            ShowStatus($"Unable to delete: {ex.Message}");
            return;
        }

        HideStatus();
        LoadDirectory(CurrentDirectory);
    }

    private InteractiveTextField CreateGoToField()
    {
        var field = new InteractiveTextField();
        field.Bind(Key.Enter, (_, _) => TryApplyGoTo());
        field.Bind(Key.Esc, (_, _) =>
        {
            HideGoTo();
            return true;
        });
        field.Bind(Key.Tab, (_, _) => TryCompleteGoTo());
        field.Bind(Key.Tab.WithShift, (_, _) => TryCompleteGoTo());
        return field;
    }

    private void BeginGoTo()
    {
        if (_goToField is null)
        {
            return;
        }

        _goToField.Visible = true;
        _goToField.Text = EnsureTrailingSeparator(_currentDirectory);
        _goToField.MoveEnd();
        _goToField.SetFocus();
        RefreshLayout();
    }

    private void HideGoTo()
    {
        if (_goToField is null)
        {
            return;
        }

        _goToField.Visible = false;
        RefreshLayout();
        FocusList();
    }

    private bool TryApplyGoTo()
    {
        if (_goToField is null)
        {
            return true;
        }

        string input = _goToField.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            HideGoTo();
            return true;
        }

        string target = ResolveAbsolutePath(input);
        if (!Directory.Exists(target))
        {
            ShowStatus("Directory does not exist.");
            return true;
        }

        LoadDirectory(target);
        HideGoTo();
        return true;
    }

    private string ResolveAbsolutePath(string input)
    {
        string expanded = ExpandHome(input);
        string candidate = string.IsNullOrWhiteSpace(expanded) ? _currentDirectory : expanded;

        if (!Path.IsPathRooted(candidate))
        {
            candidate = Path.Combine(_currentDirectory, candidate);
        }

        try
        {
            return Path.GetFullPath(candidate);
        }
        catch
        {
            return candidate;
        }
    }

    private static string ExpandHome(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        if (input.StartsWith("~"))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (input.Length == 1)
            {
                return home;
            }

            char sep = Path.DirectorySeparatorChar;
            char altSep = Path.AltDirectorySeparatorChar;
            if (input[1] == sep || input[1] == altSep)
            {
                string remainder = input.Length > 2 ? input[2..] : string.Empty;
                return Path.Combine(home, remainder);
            }
        }

        return input;
    }

    private bool TryCompleteGoTo()
    {
        if (_goToField is null)
        {
            return true;
        }

        string input = _goToField.Text ?? string.Empty;
        string normalized = ResolveAbsolutePath(input);

        string baseDir;
        string partial;

        if (EndsWithSeparator(input))
        {
            baseDir = normalized;
            partial = string.Empty;
        }
        else
        {
            baseDir = Path.GetDirectoryName(normalized) ?? normalized;
            partial = Path.GetFileName(normalized);
        }

        if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
        {
            return true;
        }

        string[] candidates;
        try
        {
            candidates = Directory.GetDirectories(baseDir)
                .Where(dir => Path.GetFileName(dir).StartsWith(partial ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        catch
        {
            return true;
        }

        if (candidates.Length == 0)
        {
            return true;
        }

        string completion;
        if (candidates.Length == 1)
        {
            completion = EnsureTrailingSeparator(candidates[0]);
        }
        else
        {
            string shared = GetCommonPrefix(candidates.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name))!);
            if (!string.IsNullOrEmpty(shared) && shared.Length > (partial?.Length ?? 0))
            {
                completion = EnsureTrailingSeparator(Path.Combine(baseDir, shared));
            }
            else
            {
                completion = EnsureTrailingSeparator(candidates[0]);
            }
        }

        _goToField.Text = completion;
        _goToField.MoveEnd();
        return true;
    }

    private static bool EndsWithSeparator(string value)
        => value.EndsWith(Path.DirectorySeparatorChar) || value.EndsWith(Path.AltDirectorySeparatorChar);

    private static string EnsureTrailingSeparator(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static string GetCommonPrefix(IEnumerable<string?> names)
    {
        string[] values = names.Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .ToArray();
        if (values.Length == 0)
        {
            return string.Empty;
        }

        string prefix = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            prefix = CommonPrefix(prefix, values[i]);
            if (prefix.Length == 0)
            {
                break;
            }
        }

        return prefix;
    }

    private static string CommonPrefix(string a, string b)
    {
        int length = Math.Min(a.Length, b.Length);
        int index = 0;

        while (index < length && char.ToLowerInvariant(a[index]) == char.ToLowerInvariant(b[index]))
        {
            index++;
        }

        return a[..index];
    }

    protected void ApplyFieldTheme(InteractiveTextField? field)
    {
        if (field is null || Theme is null)
        {
            return;
        }

        Color background = ColorResolver.Resolve(Theme.Surface);
        Color foreground = ColorResolver.Resolve(Theme.OnSurface);
        field.ApplyTheme(background, foreground);
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
            FileEntryKind.File => "[info]",
            _ => string.Empty
        };

        string name = string.IsNullOrEmpty(nameColor) ? entry.Name : $"{nameColor}{entry.Name}[/]";
        string description = entry.Kind switch
        {
            FileEntryKind.Directory => "[secondary]Directory[/]",
            FileEntryKind.Parent => "[secondary]Parent directory[/]",
            _ => $"[secondary]{FormatSize(entry.Size)}  •  {GetMimeTypeDescription(entry.Extension)}[/]"
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

    protected static string FormatSize(long? size)
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

    protected static string GetMimeTypeDescription(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "Unknown type";
        }

        string normalized = extension.StartsWith('.') ? extension : "." + extension;
        string mime = MimeTypes.GetMimeType(normalized);
        return string.IsNullOrWhiteSpace(mime) ? "Unknown type" : mime;
    }

    protected sealed record FileBrowserEntry(
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

    protected enum FileEntryKind
    {
        Parent,
        Directory,
        File
    }
}
