using System.Collections.ObjectModel;
using System.Globalization;
using Straumr.Console.Tui.Components.ListViews;
using Straumr.Console.Tui.Components.Prompts.Base;
using Straumr.Console.Tui.Components.Text;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Enums;
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
    private MarkupLabel? _statusLabel;
    private object? _statusTimeout;
    private Label? _filterLabel;
    private InteractiveTextField? _filterField;
    private Label? _goToLabel;
    private InteractiveTextField? _goToField;
    private Label? _newDirLabel;
    private InteractiveTextField? _newDirField;
    private FileBrowserEntry? _pendingDelete;

    private readonly string _homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private string _filterText = string.Empty;

    public required string Title { get; init; }
    public string? InitialPath { get; init; }

    public event Action? CancelRequested;

    protected string CurrentDirectory { get; private set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

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
        LoadDirectory(CurrentDirectory);

        return frame;
    }

    protected abstract void BuildContent(FrameView frame);

    protected virtual void OnAfterDirectoryChanged() { }

    protected virtual bool HandleCustomKey(Key key) => false;

    protected virtual void OnFileEntryActivated(FileBrowserEntry entry) { }

    protected virtual void OnDirectoryEntryActivated(FileBrowserEntry entry)
    {
        LoadDirectory(entry.FullPath);
    }

    protected virtual void OnParentEntryActivated(FileBrowserEntry entry)
    {
        DirectoryInfo current = new(CurrentDirectory);
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
        CurrentDirectory = directory;
        UpdatePathLabel();
    }

    protected void ShowStatus(string message) => ShowStatusMarkup(message, autoHide: false);

    private void ShowStatusMarkup(string markup, bool autoHide)
    {
        if (_statusLabel is null)
        {
            return;
        }

        CancelStatusTimeout();
        _statusLabel.Markup = markup;
        _statusLabel.Visible = true;

        if (autoHide)
        {
            _statusTimeout = _statusLabel.App?.AddTimeout(TimeSpan.FromSeconds(3), () =>
            {
                HideStatus();
                return false;
            });
        }
    }

    protected void HideStatus()
    {
        CancelStatusTimeout();

        _statusLabel?.Visible = false;
    }

    private void CancelStatusTimeout()
    {
        if (_statusTimeout is null)
        {
            return;
        }

        _statusLabel?.App?.RemoveTimeout(_statusTimeout);
        _statusTimeout = null;
    }

    protected virtual void InitializeStartingDirectory()
    {
        if (string.IsNullOrWhiteSpace(_homeDirectory))
        {
            CurrentDirectory = Environment.CurrentDirectory;
            return;
        }

        CurrentDirectory = TryResolveDirectory(_homeDirectory) ?? Environment.CurrentDirectory;
    }

    protected void ResetToHomeDirectory()
    {
        SetCurrentDirectory(TryResolveDirectory(_homeDirectory) ?? Environment.CurrentDirectory);
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

        CurrentDirectory = target;
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

        _filterField = TextFieldFactory.CreateFilterField(OnFilterChanged, FocusList);
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

        _statusLabel = new MarkupLabel
        {
            X = 1,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(2),
            Height = 1,
            Visible = false,
            Theme = Theme,
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

        _emptyLabel?.Visible = _filteredEntries.Count == 0;
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
                case 'c':
                case 'C':
                    if (!string.IsNullOrEmpty(_filterText))
                    {
                        ClearFilter();
                        ApplyEntryFilter();
                        RefreshLayout();
                    }
                    return true;
                case 'D':
                    BeginDelete();
                    return true;
            }
        }

        if (HandleCustomKey(key))
        {
            return true;
        }

        if (KeyHelpers.IsEnter(key))
        {
            ActivateSelection();
            return true;
        }

        if (KeyHelpers.IsTabForward(key) && (_goToField is null || !_goToField.Visible))
        {
            OnTabPressed();
            return true;
        }

        if (key == Key.Backspace)
        {
            NavigateUp();
            return true;
        }

        if (KeyHelpers.IsEscape(key))
        {
            RequestCancel();
            return true;
        }

        return false;
    }

    internal bool HandleFilterKeyDown(Key key)
    {
        if (_filterField is not { HasFocus: true })
        {
            return false;
        }

        if (KeyHelpers.IsEscape(key))
        {
            ClearFilter();
            FocusList();
            return true;
        }

        if (KeyHelpers.IsEnter(key))
        {
            FocusList();
            return true;
        }

        return false;
    }

    internal bool HandleGoToKeyDown(Key key)
    {
        if (_goToField is not { HasFocus: true })
        {
            return false;
        }

        if (KeyHelpers.IsEscape(key))
        {
            HideGoTo();
            return true;
        }

        if (KeyHelpers.IsEnter(key))
        {
            TryApplyGoTo();
            return true;
        }

        return false;
    }

    protected virtual void OnTabPressed()
    {
        FocusList();
    }

    private void RequestCancel() => CancelRequested?.Invoke();

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
        int? selectedRow = _listView?.SelectedItem;
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
        int? selectedRow = _listView?.SelectedItem;
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
        DirectoryInfo current = new(CurrentDirectory);
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
                                                       || !string.IsNullOrEmpty(_filterField.Text));

        _filterLabel?.Visible = showFilter;

        _filterField?.Visible = showFilter;

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

    private void UpdatePathLabel()
    {
        _pathLabel?.Text = $"Directory: {CurrentDirectory}";
    }

    private static string? TryResolveDirectory(string value)
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
        field.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsEnter(key), (_, _) => TryCreateNewDirectory()));
        field.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsEscape(key), (_, _) =>
        {
            HideNewDir();
            return true;
        }));
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

        string name = _newDirField.Text.Trim();
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

        ClearFilter();
        LoadDirectory(CurrentDirectory);
        SelectEntryByPath(target);
        HideNewDir();
        return true;
    }

    private void SelectEntryByPath(string fullPath)
    {
        if (_listView is null)
        {
            return;
        }

        for (int i = 0; i < _filteredEntries.Count; i++)
        {
            if (string.Equals(_filteredEntries[i].FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                _listView.SelectedItem = i * MarkupLabelListDataSource.RowsPerItem;
                return;
            }
        }
    }

    private void ClearFilter()
    {
        _filterText = string.Empty;
        _filterField?.Text = string.Empty;
    }

    private void BeginDelete()
    {
        FileBrowserEntry? entry = GetSelectedEntry();
        if (entry is null || entry.Kind == FileEntryKind.Parent)
        {
            ShowStatusMarkup("[warning]Select a file or directory to delete.[/]", autoHide: true);
            return;
        }

        _pendingDelete = entry;
        string kind = entry.Kind == FileEntryKind.Directory ? "directory" : "file";
        ShowStatus($"Delete {kind} '{entry.Name}'? Press y to confirm, any other key to cancel.");
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
            if (entry.Kind == FileEntryKind.Directory)
            {
                Directory.Delete(entry.FullPath, recursive: true);
            }
            else
            {
                File.Delete(entry.FullPath);
            }
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
        field.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsEnter(key), (_, _) => TryApplyGoTo()));
        field.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsEscape(key), (_, _) =>
        {
            HideGoTo();
            return true;
        }));
        field.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsTabNavigation(key), (_, _) => TryCompleteGoTo()));
        return field;
    }

    private void BeginGoTo()
    {
        if (_goToField is null)
        {
            return;
        }

        _goToField.Visible = true;
        _goToField.Text = EnsureTrailingSeparator(CurrentDirectory);
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

        string input = _goToField.Text;
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
        string candidate = string.IsNullOrWhiteSpace(expanded) ? CurrentDirectory : expanded;

        if (!Path.IsPathRooted(candidate))
        {
            candidate = Path.Combine(CurrentDirectory, candidate);
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

        string input = _goToField.Text;
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
                .Where(dir => Path.GetFileName(dir).StartsWith(partial, StringComparison.OrdinalIgnoreCase))
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
            string shared =
                GetCommonPrefix(candidates.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name)));
            if (!string.IsNullOrEmpty(shared) && shared.Length > partial.Length)
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
            : entry.ModifiedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ??
              string.Empty;

        if (!string.IsNullOrEmpty(modified))
        {
            modified = $"[secondary]Modified {modified}[/]";
        }

        return $"◇ {glyph} {name}\n    {description}\n    {modified}";
    }

    private static string FormatSize(long? size)
    {
        if (size is null or < 0)
        {
            return "Unknown size";
        }

        double bytes = size.Value;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unitIndex = 0;

        while (bytes >= 1024 && unitIndex < units.Length - 1)
        {
            bytes /= 1024;
            unitIndex++;
        }

        return $"{bytes:0.##} {units[unitIndex]}";
    }

    private static string GetMimeTypeDescription(string extension)
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
}
