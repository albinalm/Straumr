using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal abstract class BrowserDialog
{
    private const int DialogWidth = 72;
    private const int DialogHeight = 22;

    private const int MinimumDialogHeight = 15;

    private const int ViewportMargin = 2;

    private const int HintRows = 3;

    private const int ContentCountCap = 1000;

    private const string ParentGlyph = "↑ ";

    private const string TargetGlyph = "→ ";
    private const string FolderGlyph = "▸ ";
    private const string FileGlyph = "  ";

    private static readonly Thickness NoticeInset = new(1, 0, 0, 0);
    private readonly State<string> _currentPath;
    private readonly Dialog _dialog;

    private readonly State<int> _entryCount = new(0);
    private readonly State<string?> _error = new(null);
    private readonly ResourceFilter _filter;
    private readonly ResourceList _folderList;
    private readonly State<bool> _isEditingLocation = new(false);
    private readonly BrowserLocationInput _locationInput;

    private readonly State<int> _matchCount = new(0);
    private readonly string _sectionTitle;

    private readonly Action<string> _select;
    private readonly bool _selectsFiles;

    private List<BrowserDirectoryEntryModel> _entries = [];
    private List<BrowserDirectoryEntryModel> _visibleEntries = [];

    protected BrowserDialog(
        string? requestedPath,
        string? defaultPath,
        Action<string> select,
        string title,
        string confirmLabel,
        bool selectsFiles)
    {
        _select = select;
        _selectsFiles = selectsFiles;
        _sectionTitle = selectsFiles ? "Files" : "Folders";
        _currentPath = new State<string>(ResolveStartDirectory(requestedPath, defaultPath));

        _filter = new ResourceFilter(
            selectsFiles ? "filter files" : "filter folders",
            ApplyFilter,
            () => _folderList);

        _folderList = new ResourceList([], activateLabel: "Open")
        {
            AutoFocus = true
        };
        _folderList.ItemActivated += index => Activate(_visibleEntries[index]);
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Up",
            LabelMarkup = "Up",
            Gesture = TuiKeybindHelpers.Get("BrowserDialog.Up"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => Directory.GetParent(_currentPath.Value) is not null,
            CanExecute = _ => Directory.GetParent(_currentPath.Value) is not null,
            Execute = _ => OpenParent()
        });

        _locationInput = new BrowserLocationInput(
            () => _currentPath.Value,
            NavigateTo,
            CancelLocationEditor,
            message => _error.Value = message,
            text => PathCompletionHelpers.Files(
                text,
                _currentPath.Value,
                _selectsFiles ? IncludeFile : _ => false))
        {
            IsVisible = false,
            IsTabStop = false,
            HorizontalAlignment = Align.Stretch
        };
        _locationInput.SetStyle(StraumrStyleService.TextBox);
        _locationInput.RemoveCommand("TextEditor.Undo");
        _locationInput.RemoveCommand("TextEditor.Redo");

        var cancelButton = new Button("Cancel");
        cancelButton.SetStyle(StraumrStyleService.Button);

        var selectButton = new Button(confirmLabel);
        selectButton.SetStyle(StraumrStyleService.PrimaryButton);
        selectButton.IsEnabled(() => CanConfirmSelection);
        selectButton.Click(ConfirmSelection);

        var hints = new HintBar();
        hints.SetStyle(StraumrStyleService.CommandBar);

        Visual content = BuildContent(
            hints,
            StraumrSurfaceHelpers.Bar(
                BuildTargetLine(),
                new HStack(cancelButton, selectButton)
                    .Spacing(1)
                    .HorizontalAlignment(Align.End)));

        _filter.AttachCommands(_folderList);
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Select",
            LabelMarkup = "Select",
            Gesture = TuiKeybindHelpers.Get("BrowserDialog.Select"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => CanConfirmSelection,
            CanExecute = _ => CanConfirmSelection,
            Execute = _ => ConfirmSelection()
        });

        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Select.Current",
            LabelMarkup = "Select this folder",
            Gesture = TuiKeybindHelpers.Get("BrowserDialog.Select.Current"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.None,
            IsVisible = _ => !_selectsFiles,
            CanExecute = _ => !_selectsFiles,
            Execute = _ => Confirm(_currentPath.Value)
        });
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.New",
            LabelMarkup = "New",
            Gesture = TuiKeybindHelpers.Get("BrowserDialog.New"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => TuiKeybindHelpers.Run("BrowserDialog.New", ShowNewFolderPrompt)
        });
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Rename",
            LabelMarkup = "Rename",
            Gesture = TuiKeybindHelpers.Get("BrowserDialog.Rename"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => EditableEntry is not null,
            CanExecute = _ => EditableEntry is not null,
            Execute = _ => TuiKeybindHelpers.Run("BrowserDialog.Rename", ShowRenamePrompt)
        });
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Delete",
            LabelMarkup = "Delete",
            Gesture = TuiKeybindHelpers.Get("BrowserDialog.Delete"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => EditableEntry is not null,
            CanExecute = _ => EditableEntry is not null,
            Execute = _ => ShowDeleteConfirmation()
        });

        foreach (Command command in BuildLocationCommands())
        {
            content.AddCommand(command);
        }

        _dialog = StraumrDialogHelpers.Create(
            new TextBlock(title).Style(StraumrStyleService.AccentText),
            content,
            DialogWidth);
        _dialog.Height(ComputeHeight);

        _dialog.RemoveCommand(StraumrDialogHelpers.CancelCommandId);
        _dialog.AddCommand(new Command
        {
            Id = "BrowserDialog.Cancel",
            LabelMarkup = "Cancel",
            Gesture = TuiKeybindHelpers.Get("BrowserDialog.Cancel"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => !IsEditingText,
            CanExecute = _ => !IsEditingText,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => _dialog.Close()
        });

        cancelButton.Click(() => _dialog.Close());

    }


    private bool IsEditingText =>
        _dialog.App?.FocusedElement is { } focused &&
        (ReferenceEquals(focused, _filter.Root) || ReferenceEquals(focused, _locationInput));

    private BrowserDirectoryEntryModel? EditableEntry =>
        SelectedEntry is { IsParent: false, IsDrive: false, IsFile: false } entry ? entry : null;

    private BrowserDirectoryEntryModel? SelectedEntry =>
        (uint)_folderList.SelectedIndex < (uint)_visibleEntries.Count
            ? _visibleEntries[_folderList.SelectedIndex]
            : null;

    private bool CanConfirmSelection =>
        !_selectsFiles || SelectedEntry?.IsFile == true;

    private string SelectionTarget =>
        !_selectsFiles
            ? SelectedEntry is { IsParent: false } entry ? entry.Path : _currentPath.Value
            : SelectedEntry is { IsFile: true } file ? file.Path : "Select a file.";

    protected virtual bool IncludeFile(string path) => false;

    public void Show()
    {
        LoadDirectory(_currentPath.Value);
        TuiWindowHelpers.Show(_dialog, () => _folderList.App?.Focus(_folderList));
    }

    private int? ComputeHeight() =>
        Math.Clamp(TerminalViewportHelpers.Rows(_dialog) - ViewportMargin, MinimumDialogHeight, DialogHeight);

    private IEnumerable<Command> BuildLocationCommands()
    {
        yield return BuildLocationCommand(
            "BrowserDialog.Location",
            CommandPresentation.CommandBar);
        yield return BuildLocationCommand(
            "BrowserDialog.Location.Letter",
            CommandPresentation.None);
    }

    private Command BuildLocationCommand(
        string id,
        CommandPresentation presentation) =>
        new()
        {
            Id = id,
            LabelMarkup = "Path",
            Gesture = TuiKeybindHelpers.Get(id),
            Importance = CommandImportance.Secondary,
            Presentation = presentation,
            CanExecute = _ => !_isEditingLocation.Value,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => OpenLocationEditor()
        };

    private Visual BuildContent(Visual hints, Visual actions) =>
        new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Fixed(HintRows) },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(BuildLocationBar(), 0, 0)
            .Cell(_filter.Root, 1, 0)
            .Cell(
                StraumrSurfaceHelpers.TitledDivider(_sectionTitle, () => _folderList.HasFocusWithin),
                2,
                0)
            .Cell(
                StraumrSurfaceHelpers.Inset(
                    new TextBlock(() => Notice() ?? string.Empty)
                        .Style(() => _error.Value is not null
                            ? StraumrStyleService.RedText
                            : StraumrStyleService.MutedText)
                        .Trimming(TextTrimming.EndEllipsis)
                        .Wrap(true)
                        .IsVisible(() => Notice() is not null),
                    NoticeInset),
                3,
                0)
            .Cell(ResourceScreenLayoutHelpers.Scrollable(_folderList), 4, 0)
            .Cell(StraumrSurfaceHelpers.HorizontalDivider(), 5, 0)
            .Cell(hints, 6, 0)
            .Cell(actions, 7, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    private Visual BuildTargetLine() =>
        new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Cell(new TextBlock(TargetGlyph).Style(StraumrStyleService.MutedText), 0, 0)
            .Cell(
                new TextBlock(() => PathFormatting.Display(SelectionTarget))
                    .Style(StraumrStyleService.MutedBrightText)
                    .Trimming(TextTrimming.StartEllipsis)
                    .HorizontalAlignment(Align.Stretch),
                0,
                1)
            .HorizontalAlignment(Align.Stretch);

    private Visual BuildLocationBar() =>
        StraumrSurfaceHelpers.Bar(
            new ZStack(
                    new TextBlock(() => PathFormatting.Display(_currentPath.Value))
                        .Style(StraumrStyleService.MutedBrightText)
                        .Trimming(TextTrimming.StartEllipsis)
                        .HorizontalAlignment(Align.Stretch)
                        .IsVisible(() => !_isEditingLocation.Value),
                    _locationInput)
                .HorizontalAlignment(Align.Stretch),
            new TextBlock(() => $" {CountLabel()} ").Style(StraumrStyleService.TokenChip));

    private string CountLabel() =>
        _filter.Text.Trim().Length == 0
            ? _entryCount.Value.ToString()
            : $"{_matchCount.Value}/{_entryCount.Value}";

    private string? Notice()
    {
        if (_error.Value is not null)
        {
            return _error.Value;
        }

        string query = _filter.Text.Trim();
        if (query.Length > 0)
        {
            return _matchCount.Value == 0 ? $"No {_sectionTitle.ToLowerInvariant()} match {query}." : null;
        }

        return _entryCount.Value == 0
            ? !_selectsFiles
                ? "This folder has no subfolders."
                : "This folder has no matching files or subfolders."
            : null;
    }

    private void LoadDirectory(string path, string? preferredPath = null)
    {
        List<BrowserDirectoryEntryModel> entries = new();
        string? error = null;

        try
        {
            DirectoryInfo? parent = Directory.GetParent(path);
            if (parent is not null)
            {
                entries.Add(BrowserDirectoryEntryModel.Parent(parent.FullName));
            }
            else if (OperatingSystem.IsWindows())
            {
                entries.AddRange(ReadyDriveRoots(path));
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
        }

        try
        {
            entries.AddRange(Directory
                .EnumerateDirectories(path)
                .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
                .Select(BrowserDirectoryEntryModel.Folder));
            if (_selectsFiles)
            {
                entries.AddRange(Directory
                    .EnumerateFiles(path)
                    .Where(IncludeFile)
                    .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                    .Select(BrowserDirectoryEntryModel.File));
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            error ??= exception.Message;
        }

        _error.Value = error;
        _currentPath.Value = path;
        _entries = entries;
        _entryCount.Value = entries.Count(entry => !entry.IsParent);

        if (_filter.Text.Length > 0)
        {
            _filter.Clear();
        }
        else
        {
            ApplyFilter(string.Empty);
        }

        SelectPath(preferredPath);
    }

    private void ApplyFilter(string text)
    {
        BrowserDirectoryEntryModel? selected = SelectedEntry;
        string query = text.Trim();

        _visibleEntries = query.Length == 0
            ? [.. _entries]
            : _entries
                .Where(entry =>
                    entry.IsParent ||
                    entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        _matchCount.Value = _visibleEntries.Count(entry => !entry.IsParent);
        _folderList.SetRows(_visibleEntries.Select(ToRow));
        SelectPath(selected?.Path);
    }

    private ResourceRowModel ToRow(BrowserDirectoryEntryModel entry) =>
        new(entry.IsParent
            ? $"{ParentGlyph}{entry.Name}"
            : !_selectsFiles
                ? entry.Name
                : $"{(entry.IsFile ? FileGlyph : FolderGlyph)}{entry.Name}");

    private void SelectPath(string? path)
    {
        int index = path is null
            ? -1
            : _visibleEntries.FindIndex(entry => PathEquals(entry.Path, path));

        if (index < 0 && _selectsFiles)
        {
            index = _visibleEntries.FindIndex(entry => entry.IsFile);
        }

        if (index < 0)
        {
            index = _visibleEntries.FindIndex(entry => !entry.IsParent);
        }

        _folderList.SelectedIndex = index >= 0
            ? index
            : _visibleEntries.Count > 0 ? 0 : -1;
    }

    private void ShowNewFolderPrompt()
    {
        string parent = _currentPath.Value;
        var prompt = new TextPromptDialog(
            "New folder",
            $"Name, inside {PathFormatting.Display(parent)}",
            string.Empty,
            "Create",
            name => ValidateName(name, parent),
            name => CreateFolder(parent, name))
        {
            PendingEcho = TuiKeybindHelpers.OpeningEcho
        };
        prompt.Show();
    }

    private void ShowRenamePrompt()
    {
        if (EditableEntry is not { } entry)
        {
            return;
        }

        var prompt = new TextPromptDialog(
            "Rename folder",
            $"New name for {entry.Name}",
            entry.Name,
            "Rename",
            name => name == entry.Name
                ? null
                : ValidateName(name, _currentPath.Value),
            name => RenameFolder(entry, name))
        {
            PendingEcho = TuiKeybindHelpers.OpeningEcho
        };
        prompt.Show();
    }

    private void ShowDeleteConfirmation()
    {
        if (EditableEntry is not { } entry)
        {
            return;
        }

        (int folders, int files, bool capped) = CountContents(entry.Path);
        string contents = folders + files == 0
            ? "It is empty."
            : capped
                ? $"Over {ContentCountCap} items inside it will be permanently removed."
                : $"{CountFormatting.Label(folders, "folder")} and " +
                  $"{CountFormatting.Label(files, "file")} inside it will be permanently removed.";

        new ConfirmDialog(
                "Delete folder",
                $"Delete {entry.Name}?",
                contents,
                "Delete",
                true,
                () => DeleteFolder(entry))
            .Show();
    }

    private void CreateFolder(string parent, string name)
    {
        string path = Path.Combine(parent, name);
        if (!TryFileSystem(() => Directory.CreateDirectory(path)))
        {
            return;
        }

        LoadDirectory(parent, path);
    }

    private void RenameFolder(BrowserDirectoryEntryModel entry, string name)
    {
        string path = Path.Combine(_currentPath.Value, name);
        if (!TryFileSystem(() => Directory.Move(entry.Path, path)))
        {
            return;
        }

        LoadDirectory(_currentPath.Value, path);
    }

    private void DeleteFolder(BrowserDirectoryEntryModel entry)
    {
        string? survivor = NearestSurvivor(entry);

        if (!TryFileSystem(() => Directory.Delete(entry.Path, true)))
        {
            return;
        }

        LoadDirectory(_currentPath.Value, survivor);
    }

    private string? NearestSurvivor(BrowserDirectoryEntryModel deleted)
    {
        int index = _visibleEntries.FindIndex(entry => PathEquals(entry.Path, deleted.Path));
        if (index < 0)
        {
            return null;
        }

        return index + 1 < _visibleEntries.Count
            ? _visibleEntries[index + 1].Path
            : index > 0
                ? _visibleEntries[index - 1].Path
                : null;
    }

    private bool TryFileSystem(Action change)
    {
        try
        {
            change();
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
                NotSupportedException or PathTooLongException)
        {
            _error.Value = exception.Message;
            return false;
        }
    }

    private static string? ValidateName(string name, string parent)
    {
        if (name.Length == 0)
        {
            return "Name is required.";
        }

        if (name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            return "Name cannot contain a path separator.";
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "Name contains a reserved character.";
        }

        if (name is "." or "..")
        {
            return "Name cannot be . or ..";
        }

        string path = Path.Combine(parent, name);
        if (Directory.Exists(path) || File.Exists(path))
        {
            return $"{name} already exists here.";
        }

        return null;
    }

    private static (int Folders, int Files, bool Capped) CountContents(string path)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };

        try
        {
            int folders = Directory.EnumerateDirectories(path, "*", options)
                .Take(ContentCountCap + 1)
                .Count();
            int files = Directory.EnumerateFiles(path, "*", options)
                .Take(ContentCountCap + 1)
                .Count();

            return (folders, files, folders + files > ContentCountCap);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return (0, 0, false);
        }
    }

    private void Activate(BrowserDirectoryEntryModel entry)
    {
        if (entry.IsFile)
        {
            Confirm(entry.Path);
        }
        else
        {
            LoadDirectory(entry.Path, entry.IsParent ? _currentPath.Value : null);
        }
    }

    private void OpenParent()
    {
        if (Directory.GetParent(_currentPath.Value) is { } parent)
        {
            LoadDirectory(parent.FullName, _currentPath.Value);
        }
    }

    private void OpenLocationEditor()
    {
        _isEditingLocation.Value = true;
        _locationInput.PendingEcho = TuiKeybindHelpers.Echo("BrowserDialog.Location");
        _locationInput.Activate();
    }

    private void CloseLocationEditor()
    {
        _locationInput.Deactivate();
        _isEditingLocation.Value = false;
        _folderList.App?.Focus(_folderList);
    }

    private void CancelLocationEditor()
    {
        _error.Value = null;
        CloseLocationEditor();
    }

    private void NavigateTo(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path, _currentPath.Value);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _error.Value = exception.Message;
            return;
        }

        if (_selectsFiles && File.Exists(fullPath) && IncludeFile(fullPath))
        {
            Confirm(fullPath);
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            _error.Value = !_selectsFiles
                ? "Directory does not exist."
                : "Folder or matching file does not exist.";
            return;
        }

        LoadDirectory(fullPath);
        CloseLocationEditor();
    }

    private void ConfirmSelection()
    {
        if (CanConfirmSelection)
        {
            Confirm(SelectionTarget);
        }
    }

    private void Confirm(string path)
    {
        _dialog.Close();
        _select(path);
    }

    private static IEnumerable<BrowserDirectoryEntryModel> ReadyDriveRoots(string currentPath)
    {
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            bool isReady;
            try
            {
                isReady = drive.IsReady;
            }
            catch (IOException)
            {
                continue;
            }

            if (isReady && !PathEquals(drive.RootDirectory.FullName, currentPath))
            {
                yield return BrowserDirectoryEntryModel.Drive(drive.RootDirectory.FullName);
            }
        }
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string ResolveStartDirectory(string? requestedPath, string? defaultPath)
    {
        foreach (string? candidate in new[]
                 {
                     requestedPath,
                     defaultPath,
                     Environment.CurrentDirectory,
                     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                 })
        {
            string? existing = FindExistingDirectory(candidate);
            if (existing is not null)
            {
                return existing;
            }
        }

        return Path.GetPathRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
    }

    private static string? FindExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            string? candidate = Path.GetFullPath(path.Trim());
            while (!string.IsNullOrEmpty(candidate))
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                candidate = Path.GetDirectoryName(candidate);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return null;
    }
}
