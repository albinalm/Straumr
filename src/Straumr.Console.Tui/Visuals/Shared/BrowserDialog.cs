using Straumr.Console.Tui.Formatting;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// Shared cross-platform filesystem browser behavior. Version 3.9.0 has no file or folder dialog,
/// and a native one would cost portability, Native AOT and the TUI's dependency isolation, so this
/// composes the framework's modal with the app's own resource list, inline filter and command bar.
/// </summary>
/// <remarks>
/// It mirrors a resource screen's left panel deliberately: a bar carrying the location and a count
/// badge, the rule closing it carrying the section title, then the list. So the focus chip travels
/// the same rule here as it does on a screen, and nothing about the list has to be learned twice.
/// <para>
/// It knows nothing about workspaces, which is what keeps it reusable by any caller that needs a
/// folder. It therefore cannot tell that a folder holds one, and its rename and delete are as
/// unguarded as the operating system's own picker: the registry stores absolute paths and can be
/// broken from a file manager just as easily, so guarding here would buy safety nowhere.
/// </para>
/// </remarks>
internal abstract class BrowserDialog
{
    private const int DialogWidth = 72;
    private const int DialogHeight = 22;

    /// <summary>
    /// Height below which the dialog stops shrinking. Under this the chrome alone fills it and the
    /// folder list has no rows left, so clipping is preferable to a browser with nothing to browse.
    /// </summary>
    private const int MinimumDialogHeight = 15;

    /// <summary>Rows left to the screen around the dialog so it never sits flush against the frame.</summary>
    private const int ViewportMargin = 2;

    /// <summary>
    /// Rows reserved for the hint bar. It is fixed rather than sized to its content because the
    /// number of hints changes with focus, and letting the bar grow and shrink moved the folder list
    /// under the cursor every time <c>Tab</c> was pressed.
    /// </summary>
    private const int HintRows = 3;

    private const char LocationLetter = 'l';

    /// <summary>
    /// Confirms the highlighted folder. A plain letter is what gets advertised, because a terminal
    /// without the kitty keyboard protocol sends <c>Ctrl+Enter</c> as a bare <c>Enter</c> and would
    /// descend into the folder instead; that gesture is kept as an unpresented alias for the
    /// terminals that do report the modifier.
    /// </summary>
    private const char SelectGesture = 's';
    private const char NewFolderGesture = 'n';
    private const char RenameGesture = 'r';
    private const char DeleteGesture = 'd';

    /// <summary>
    /// Entries counted before a delete confirmation stops counting and says "over this many". A
    /// recursive count runs from a keystroke, so it has to be bounded: the number is there to convey
    /// scale, and past a thousand items the exact figure changes nothing about the decision.
    /// </summary>
    private const int ContentCountCap = 1000;

    /// <summary>Leads the row that walks out of the current folder, followed by the parent's name.</summary>
    private const string ParentGlyph = "↑ ";

    /// <summary>Leads the path that confirming will return, beside the button that returns it.</summary>
    private const string TargetGlyph = "→ ";
    private const string FolderGlyph = "▸ ";
    private const string FileGlyph = "  ";

    /// <summary>Lines the notice up with the section title it sits under.</summary>
    private static readonly Thickness NoticeInset = new(1, 0, 0, 0);

    private readonly Action<string> _select;
    private readonly bool _selectsFiles;
    private readonly string _sectionTitle;
    private readonly Dialog _dialog;
    private readonly ResourceList _folderList;
    private readonly LocationInput _locationInput;
    private readonly ResourceFilter _filter;
    private readonly State<string> _currentPath;
    private readonly State<string?> _error = new(null);
    private readonly State<bool> _isEditingLocation = new(false);

    /// <summary>
    /// Counts kept as state because the badge reads them: a plain field would leave it showing the
    /// previous folder's total until something else happened to invalidate the bar.
    /// </summary>
    private readonly State<int> _entryCount = new(0);

    private readonly State<int> _matchCount = new(0);

    private List<DirectoryEntry> _entries = [];
    private List<DirectoryEntry> _visibleEntries = [];

    /// <param name="title">Names the dialog, so a caller choosing an export target can say so.</param>
    /// <param name="confirmLabel">The confirming button's text, paired with the <c>s</c> gesture.</param>
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

        // The filter comes first because the list's empty message reads its query, and a computed
        // TextBlock evaluates once on construction.
        _filter = new ResourceFilter(
            selectsFiles ? "filter files" : "filter folders",
            ApplyFilter,
            () => _folderList);

        // No empty visual: the parent row keeps the list non-empty in almost every folder, so the
        // notice row below the title is what reports an empty folder or a query with no matches.
        _folderList = new ResourceList([], activateLabel: "Open")
        {
            AutoFocus = true
        };
        _folderList.ItemActivated += index => Activate(_visibleEntries[index]);
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Up",
            LabelMarkup = "Up",
            Gesture = new KeyGesture(TerminalKey.Backspace),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => Directory.GetParent(_currentPath.Value) is not null,
            CanExecute = _ => Directory.GetParent(_currentPath.Value) is not null,
            Execute = _ => OpenParent()
        });

        _locationInput = new LocationInput(
            () => _currentPath.Value,
            NavigateTo,
            CancelLocationEditor,
            message => _error.Value = message,
            text => PathCompletion.Files(
                text,
                _currentPath.Value,
                _selectsFiles ? IncludeFile : _ => false))
        {
            IsVisible = false,
            IsTabStop = false,
            HorizontalAlignment = Align.Stretch
        };
        _locationInput.SetStyle(StraumrStyles.TextBox);
        _locationInput.RemoveCommand("TextEditor.Undo");
        _locationInput.RemoveCommand("TextEditor.Redo");

        var cancelButton = new Button("Cancel");
        cancelButton.SetStyle(StraumrStyles.Button);

        var selectButton = new Button(confirmLabel);
        selectButton.SetStyle(StraumrStyles.PrimaryButton);
        selectButton.IsEnabled(() => CanConfirmSelection);
        selectButton.Click(ConfirmSelection);

        var hints = new CommandBar
        {
            MultiLine = true,
            HorizontalAlignment = Align.Stretch
        };
        hints.SetStyle(StraumrStyles.CommandBar);

        Visual content = BuildContent(
            hints,
            StraumrSurfaces.Bar(
                BuildTargetLine(),
                new HStack(cancelButton, selectButton)
                    .Spacing(1)
                    .HorizontalAlignment(Align.End)));

        // Scoped to the list, not to the whole dialog, because a plain-character gesture on an
        // ancestor fires while a text field below it has focus: `/` would have opened the filter
        // instead of reaching the path being typed, and `s` would never have reached either field.
        _filter.AttachCommands(_folderList);
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Select",
            LabelMarkup = "Select",
            Gesture = new KeyGesture(SelectGesture),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => CanConfirmSelection,
            CanExecute = _ => CanConfirmSelection,
            Execute = _ => ConfirmSelection()
        });

        // The other half of the pair: confirm the folder being stood in rather than the one under
        // the cursor, so descending into a folder and accepting it is two keys. Not presented,
        // because a terminal that does not report the modifier sends a bare Enter and will open the
        // highlighted row instead; advertising a key that does something else on some terminals is
        // worse than leaving a second way in undocumented.
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Select.Current",
            LabelMarkup = "Select this folder",
            Gesture = new KeyGesture(TerminalKey.Enter, TerminalModifiers.Ctrl),
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
            Gesture = new KeyGesture(NewFolderGesture),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => ShowNewFolderPrompt()
        });
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Rename",
            LabelMarkup = "Rename",
            Gesture = new KeyGesture(RenameGesture),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => EditableEntry is not null,
            CanExecute = _ => EditableEntry is not null,
            Execute = _ => ShowRenamePrompt()
        });
        _folderList.AddCommand(new Command
        {
            Id = "BrowserDialog.Delete",
            LabelMarkup = "Delete",
            Gesture = new KeyGesture(DeleteGesture),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => EditableEntry is not null,
            CanExecute = _ => EditableEntry is not null,
            Execute = _ => ShowDeleteConfirmation()
        });

        foreach (Command command in BuildLocationCommands())
            content.AddCommand(command);

        _dialog = StraumrDialog.Create(
            new TextBlock(title).Style(StraumrStyles.AccentText),
            content,
            DialogWidth);
        _dialog.Height = DialogHeight;

        // The shared Escape closes the dialog, which is wrong while a text field owns the key and
        // wrong to advertise beside that field's own Escape. Both editors already win the gesture by
        // being nearer the focus, so this only has to stop the second hint appearing.
        _dialog.RemoveCommand(StraumrDialog.CancelCommandId);
        _dialog.AddCommand(new Command
        {
            Id = "BrowserDialog.Cancel",
            LabelMarkup = "Cancel",
            Gesture = new KeyGesture(TerminalKey.Escape),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            IsVisible = _ => !IsEditingText,
            CanExecute = _ => !IsEditingText,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => _dialog.Close()
        });

        cancelButton.Click(() => _dialog.Close());

    }

    protected virtual bool IncludeFile(string path) => false;

    /// <remarks>
    /// The height is fixed rather than sized to content, because a folder list that grew and shrank
    /// with each folder's contents would move the buttons under the pointer. It is trimmed to the
    /// viewport here instead: the dialog keeps its own width, but at the full height a short terminal
    /// pushed the hints and both buttons off screen. <c>Visual.App</c> is null until the dialog is
    /// shown, so this is the first point the viewport can be read at all.
    /// </remarks>
    public void Show()
    {
        LoadDirectory(_currentPath.Value);
        _dialog.Show();

        if (_dialog.App is { } app)
        {
            _dialog.Height = Math.Clamp(
                app.Terminal.Size.Rows - ViewportMargin,
                MinimumDialogHeight,
                DialogHeight);
        }
    }

    /// <remarks>
    /// A terminal sends <c>Ctrl</c> and a letter as the single C0 byte the letter maps to, so the
    /// gesture has to carry that control character: <c>KeyGesture('l', Ctrl)</c> never matches
    /// anything, and gives itself away by rendering as a lowercase <c>Ctrl+l</c> in the command bar.
    /// Measured against version 3.9.0's decoder, the raw-byte path that Alacritty, xterm and Windows
    /// Terminal use and the CSI path the kitty keyboard protocol uses both arrive as
    /// <c>U+000C</c> + <c>Ctrl</c>, so one gesture covers both. The letter form is registered beside
    /// it anyway, unpresented, because a host that reported the letter and the modifier separately
    /// would otherwise leave an advertised key doing nothing with no way to tell why.
    /// </remarks>
    private IEnumerable<Command> BuildLocationCommands()
    {
        yield return BuildLocationCommand(
            "BrowserDialog.Location",
            CtrlChar(LocationLetter),
            CommandPresentation.CommandBar);
        yield return BuildLocationCommand(
            "BrowserDialog.Location.Letter",
            LocationLetter,
            CommandPresentation.None);
    }

    private Command BuildLocationCommand(
        string id,
        char gestureChar,
        CommandPresentation presentation) =>
        new()
        {
            Id = id,
            LabelMarkup = "Path",
            Gesture = new KeyGesture(gestureChar, TerminalModifiers.Ctrl),
            Importance = CommandImportance.Secondary,
            Presentation = presentation,
            CanExecute = _ => !_isEditingLocation.Value,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => OpenLocationEditor()
        };

    /// <summary>The C0 control character a terminal sends for <c>Ctrl</c> plus a letter.</summary>
    private static char CtrlChar(char letter) => (char)(char.ToUpperInvariant(letter) & 0x1F);

    /// <summary>
    /// Whether the filter or the location field owns the keyboard, which is what decides who
    /// <c>Escape</c> belongs to. It compares against <c>FocusedElement</c> rather than
    /// <c>HasFocus</c>, which mirrors it a pass later.
    /// </summary>
    private bool IsEditingText =>
        _dialog.App?.FocusedElement is { } focused &&
        (ReferenceEquals(focused, _filter.Root) || ReferenceEquals(focused, _locationInput));

    /// <remarks>
    /// The location and the filter share the bar, the title sits on the rule closing it, and the
    /// hints sit under a second rule exactly as the shell's footer does, so every surface in the
    /// dialog is one the rest of the app already taught.
    /// </remarks>
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
                StraumrSurfaces.TitledDivider(_sectionTitle, () => _folderList.HasFocusWithin),
                2,
                0)
            .Cell(
                StraumrSurfaces.Inset(
                    new TextBlock(() => Notice() ?? string.Empty)
                        .Style(() => _error.Value is not null
                            ? StraumrStyles.RedText
                            : StraumrStyles.MutedText)
                        .Trimming(TextTrimming.EndEllipsis)
                        .Wrap(true)
                        .IsVisible(() => Notice() is not null),
                    NoticeInset),
                3,
                0)
            .Cell(ResourceScreenLayout.Scrollable(_folderList), 4, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 5, 0)
            .Cell(hints, 6, 0)
            .Cell(actions, 7, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    /// <remarks>
    /// The glyph is a column of its own rather than part of the path, because the path is trimmed
    /// from the front and a leading marker is the first thing a leading ellipsis eats.
    /// </remarks>
    private Visual BuildTargetLine() =>
        new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Cell(new TextBlock(TargetGlyph).Style(StraumrStyles.MutedText), 0, 0)
            .Cell(
                new TextBlock(() => PathFormatting.Display(SelectionTarget))
                    .Style(StraumrStyles.MutedBrightText)
                    .Trimming(TextTrimming.StartEllipsis)
                    .HorizontalAlignment(Align.Stretch),
                0,
                1)
            .HorizontalAlignment(Align.Stretch);

    /// <remarks>
    /// The breadcrumb and the editor are one thing in two states, so they hold the same cell and swap
    /// on <c>IsVisible</c>. Trimming the breadcrumb from the front is what keeps the folder you are
    /// actually in on screen when the path is longer than the dialog.
    /// </remarks>
    private Visual BuildLocationBar() =>
        StraumrSurfaces.Bar(
            new ZStack(
                    new TextBlock(() => PathFormatting.Display(_currentPath.Value))
                        .Style(StraumrStyles.MutedBrightText)
                        .Trimming(TextTrimming.StartEllipsis)
                        .HorizontalAlignment(Align.Stretch)
                        .IsVisible(() => !_isEditingLocation.Value),
                    _locationInput)
                .HorizontalAlignment(Align.Stretch),
            new TextBlock(() => $" {CountLabel()} ").Style(StraumrStyles.TokenChip));

    private string CountLabel() =>
        _filter.Text.Trim().Length == 0
            ? _entryCount.Value.ToString()
            : $"{_matchCount.Value}/{_entryCount.Value}";

    /// <summary>
    /// The one line under the title that explains a list with nothing to walk into: a failed read, a
    /// folder with no subfolders, or a query that matched none of them. They share a row because only
    /// one of them can be true, and because a list showing nothing but its parent row otherwise reads
    /// as a load that failed.
    /// </summary>
    private string? Notice()
    {
        if (_error.Value is not null)
            return _error.Value;

        string query = _filter.Text.Trim();
        if (query.Length > 0)
            return _matchCount.Value == 0 ? $"No {_sectionTitle.ToLowerInvariant()} match {query}." : null;

        return _entryCount.Value == 0
            ? !_selectsFiles
                ? "This folder has no subfolders."
                : "This folder has no matching files or subfolders."
            : null;
    }

    /// <param name="preferredPath">
    /// The entry to land on. Walking up passes the folder being left, so returning through a tree
    /// puts the cursor back where it was rather than on the parent row.
    /// </param>
    /// <remarks>
    /// The rows above the folder are gathered separately from the folders themselves, so a folder
    /// whose contents cannot be read still shows the row that walks back out of it. Reporting the
    /// failure and leaving the list empty had stranded the browser in a folder it could not list.
    /// </remarks>
    private void LoadDirectory(string path, string? preferredPath = null)
    {
        var entries = new List<DirectoryEntry>();
        string? error = null;

        try
        {
            DirectoryInfo? parent = Directory.GetParent(path);
            if (parent is not null)
                entries.Add(DirectoryEntry.Parent(parent.FullName));
            else if (OperatingSystem.IsWindows())
                entries.AddRange(ReadyDriveRoots(path));
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
                .Select(DirectoryEntry.Folder));
            if (_selectsFiles)
            {
                entries.AddRange(Directory
                    .EnumerateFiles(path)
                    .Where(IncludeFile)
                    .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                    .Select(DirectoryEntry.File));
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

        // Clearing raises the filter's own text-changed path, so re-filtering has to be skipped here
        // or the rows would be built twice for every navigation.
        if (_filter.Text.Length > 0)
            _filter.Clear();
        else
            ApplyFilter(string.Empty);

        SelectPath(preferredPath);
    }

    /// <remarks>
    /// The parent row survives every query. It is the way out of a folder rather than one of its
    /// contents, and filtering it away would strand a pointer user in a folder with no matches.
    /// </remarks>
    private void ApplyFilter(string text)
    {
        DirectoryEntry? selected = SelectedEntry;
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

    private ResourceRow ToRow(DirectoryEntry entry) =>
        new(entry.IsParent
            ? $"{ParentGlyph}{entry.Name}"
            : !_selectsFiles
                ? entry.Name
                : $"{(entry.IsFile ? FileGlyph : FolderGlyph)}{entry.Name}");

    /// <remarks>
    /// With nothing to prefer, this lands on the first actual folder rather than on the row that
    /// walks back out. Opening a folder is a statement of interest in its contents, and the row-scoped
    /// commands are only offered for a real folder, so starting on the parent row hid Rename and
    /// Delete from the hint bar until the cursor was moved.
    /// </remarks>
    private void SelectPath(string? path)
    {
        int index = path is null
            ? -1
            : _visibleEntries.FindIndex(entry => PathEquals(entry.Path, path));

        if (index < 0 && _selectsFiles)
            index = _visibleEntries.FindIndex(entry => entry.IsFile);

        if (index < 0)
            index = _visibleEntries.FindIndex(entry => !entry.IsParent);

        _folderList.SelectedIndex = index >= 0
            ? index
            : _visibleEntries.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// The selected row when it is a folder this browser may act on. The parent row leads out of the
    /// folder rather than being one of its contents, and a drive root is not something rename or
    /// delete can mean anything for, so neither offers the operations.
    /// </summary>
    private DirectoryEntry? EditableEntry =>
        SelectedEntry is { IsParent: false, IsDrive: false, IsFile: false } entry ? entry : null;

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
            PendingEcho = NewFolderGesture
        };
        prompt.Show();
    }

    private void ShowRenamePrompt()
    {
        if (EditableEntry is not { } entry)
            return;

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
            PendingEcho = RenameGesture
        };
        prompt.Show();
    }

    /// <remarks>
    /// The confirmation states what the folder holds rather than asking a bare yes or no. There is no
    /// recycle bin behind this — <c>Directory.Delete</c> is final, and a portable one has no
    /// alternative — so the count is the only thing standing between a keystroke and losing a tree.
    /// </remarks>
    private void ShowDeleteConfirmation()
    {
        if (EditableEntry is not { } entry)
            return;

        (int folders, int files, bool capped) = CountContents(entry.Path);
        string contents = folders + files == 0
            ? "It is empty."
            : capped
                ? $"Over {ContentCountCap} items inside it will be permanently removed."
                : $"{CountFormatting.Label(folders, "folder")} and " +
                  $"{CountFormatting.Label(files, "file")} inside it will be permanently removed.";

        var cancelButton = new Button("Cancel")
        {
            AutoFocus = true
        };
        cancelButton.SetStyle(StraumrStyles.Button);

        var deleteButton = new Button("Delete");
        deleteButton.SetStyle(StraumrStyles.DangerButton);

        var content = new VStack(
                new TextBlock($"Delete {entry.Name}?")
                    .Style(StraumrStyles.BrightText)
                    .Wrap(true),
                new TextBlock(contents)
                    .Style(StraumrStyles.MutedText)
                    .Wrap(true),
                new HStack(cancelButton, deleteButton)
                    .Spacing(1)
                    .HorizontalAlignment(Align.End))
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        Dialog confirmation = StraumrDialog.Create(
            new TextBlock("Delete folder").Style(StraumrStyles.RedText),
            content,
            58);

        cancelButton.Click(() => confirmation.Close());
        deleteButton.Click(() =>
        {
            confirmation.Close();
            DeleteFolder(entry);
        });

        confirmation.Show();
    }

    private void CreateFolder(string parent, string name)
    {
        string path = Path.Combine(parent, name);
        if (!TryFileSystem(() => Directory.CreateDirectory(path)))
            return;

        LoadDirectory(parent, path);
    }

    private void RenameFolder(DirectoryEntry entry, string name)
    {
        string path = Path.Combine(_currentPath.Value, name);
        if (!TryFileSystem(() => Directory.Move(entry.Path, path)))
            return;

        LoadDirectory(_currentPath.Value, path);
    }

    private void DeleteFolder(DirectoryEntry entry)
    {
        // Worked out before the row disappears: staying at the same place in the list means landing
        // on whatever followed the deleted folder, or on what preceded it when it was the last.
        string? survivor = NearestSurvivor(entry);

        if (!TryFileSystem(() => Directory.Delete(entry.Path, recursive: true)))
            return;

        LoadDirectory(_currentPath.Value, survivor);
    }

    private string? NearestSurvivor(DirectoryEntry deleted)
    {
        int index = _visibleEntries.FindIndex(entry => PathEquals(entry.Path, deleted.Path));
        if (index < 0)
            return null;

        return index + 1 < _visibleEntries.Count
            ? _visibleEntries[index + 1].Path
            : index > 0
                ? _visibleEntries[index - 1].Path
                : null;
    }

    /// <summary>
    /// Runs a filesystem change, reporting a refusal on the notice row instead of throwing. Anything
    /// outside this set is a bug rather than a folder the user cannot write to, and is left to fail.
    /// </summary>
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

    /// <summary>Returns why the name cannot be used, or <see langword="null"/> when it can.</summary>
    private static string? ValidateName(string name, string parent)
    {
        if (name.Length == 0)
            return "Name is required.";

        if (name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            return "Name cannot contain a path separator.";

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Name contains a reserved character.";

        if (name is "." or "..")
            return "Name cannot be . or ..";

        string path = Path.Combine(parent, name);
        if (Directory.Exists(path) || File.Exists(path))
            return $"{name} already exists here.";

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

    private DirectoryEntry? SelectedEntry =>
        (uint)_folderList.SelectedIndex < (uint)_visibleEntries.Count
            ? _visibleEntries[_folderList.SelectedIndex]
            : null;

    private void Activate(DirectoryEntry entry)
    {
        if (entry.IsFile)
            Confirm(entry.Path);
        else
            LoadDirectory(entry.Path, entry.IsParent ? _currentPath.Value : null);
    }

    private void OpenParent()
    {
        if (Directory.GetParent(_currentPath.Value) is { } parent)
            LoadDirectory(parent.FullName, _currentPath.Value);
    }

    private void OpenLocationEditor()
    {
        // Focus is revoked from a visual that is invisible when the focus pass runs, so the editor
        // has to show itself before asking for focus rather than leaving that to a binding.
        _isEditingLocation.Value = true;
        // Only the letter form of the gesture can emit a paired text event; the control character
        // is not printable. Arming it costs nothing and covers that path.
        _locationInput.PendingEcho = LocationLetter;
        _locationInput.Activate();
    }

    /// <remarks>
    /// This deliberately leaves the notice alone. A successful jump can land in a folder that cannot
    /// be listed, and clearing here wiped that report on the way back to the list; the field's own
    /// <c>Escape</c> discards its typing error instead, which is the only case that needs it.
    /// </remarks>
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

    /// <summary>
    /// What the advertised confirm returns: the highlighted folder, which is what a native picker's
    /// button means and what saves descending into a folder only to pick it. The row that walks back
    /// out is not one of the folder's contents, so on it the answer falls back to the folder being
    /// browsed. It is shown beside the button rather than left to be inferred.
    /// </summary>
    /// <remarks>
    /// The folder being browsed is confirmable in its own right through <c>Ctrl+Enter</c>, and is
    /// already on screen as the breadcrumb. So both answers are visible at once: the top of the
    /// dialog is what <c>Ctrl+Enter</c> returns, and the line by the button is what <c>s</c> returns.
    /// </remarks>
    private bool CanConfirmSelection =>
        !_selectsFiles || SelectedEntry?.IsFile == true;

    private string SelectionTarget =>
        !_selectsFiles
            ? SelectedEntry is { IsParent: false } entry ? entry.Path : _currentPath.Value
            : SelectedEntry is { IsFile: true } file ? file.Path : "Select a file.";

    private void ConfirmSelection()
    {
        if (CanConfirmSelection)
            Confirm(SelectionTarget);
    }

    private void Confirm(string path)
    {
        _dialog.Close();
        _select(path);
    }

    private static IEnumerable<DirectoryEntry> ReadyDriveRoots(string currentPath)
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
                yield return DirectoryEntry.Drive(drive.RootDirectory.FullName);
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
                return existing;
        }

        return Path.GetPathRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory;
    }

    private static string? FindExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            string? candidate = Path.GetFullPath(path.Trim());
            while (!string.IsNullOrEmpty(candidate))
            {
                if (Directory.Exists(candidate))
                    return candidate;

                candidate = Path.GetDirectoryName(candidate);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return null;
    }

    /// <summary>
    /// The editable form of the location bar: a path with <c>Tab</c> completion over real folders.
    /// </summary>
    /// <remarks>
    /// <c>Enter</c> and <c>Escape</c> are commands rather than key handling because a framework
    /// command runs before the focused control sees the key, so the dialog's own <c>Escape</c> would
    /// otherwise close the whole picker while this field was being edited.
    /// </remarks>
    private sealed class LocationInput : TextBox
    {
        private readonly Func<string> _currentPath;
        private readonly Action<string?> _showError;
        private readonly Func<string, IReadOnlyList<string>> _complete;
        private string[] _completions = [];
        private string? _completedText;
        private int _completionIndex;

        public LocationInput(
            Func<string> currentPath,
            Action<string> navigate,
            Action close,
            Action<string?> showError,
            Func<string, IReadOnlyList<string>> complete)
        {
            _currentPath = currentPath;
            _showError = showError;
            _complete = complete;
            Focusable = false;

            AddCommand(new Command
            {
                Id = "BrowserDialog.Location.Go",
                LabelMarkup = "Go",
                Gesture = new KeyGesture(TerminalKey.Enter),
                Importance = CommandImportance.Primary,
                Presentation = CommandPresentation.CommandBar,
                Execute = _ => navigate(Text ?? string.Empty)
            });
            AddCommand(new Command
            {
                Id = "BrowserDialog.Location.Cancel",
                LabelMarkup = "Back",
                Gesture = new KeyGesture(TerminalKey.Escape),
                Importance = CommandImportance.Primary,
                Presentation = CommandPresentation.CommandBar,
                Execute = _ => close()
            });
        }

        public char? PendingEcho { get; set; }

        public void Activate()
        {
            IsVisible = true;
            Focusable = true;
            SetPath(_currentPath());
            App?.Focus(this);
        }

        public void Deactivate()
        {
            Focusable = false;
            IsVisible = false;
        }

        public void SetPath(string path)
        {
            Text = path;
            CaretIndex = path.Length;
            ResetCompletion();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == TerminalKey.Tab && e.Modifiers == TerminalModifiers.None)
            {
                Complete();
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        /// <remarks>
        /// The keystroke that opens this field arrives as a key event and an independent text event,
        /// so the gesture focuses the field and then types its own character into it. Discarding the
        /// one echoed character is order-independent, unlike deferring focus.
        /// </remarks>
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

        protected override void OnDocumentChanged(XenoAtom.Terminal.UI.Text.TextDocumentChangedEventArgs e)
        {
            base.OnDocumentChanged(e);
            _showError(null);
            if (!string.Equals(Text, _completedText, StringComparison.Ordinal))
                ResetCompletion();
        }

        /// <remarks>
        /// The framework re-asks for completions on every trigger and keeps no cycle state, so the
        /// candidate list is held here and an unchanged document is what identifies a repeat
        /// <c>Tab</c>, exactly as the command prompt does it.
        /// </remarks>
        private void Complete()
        {
            string text = Text ?? string.Empty;
            if (_completions.Length > 0 &&
                string.Equals(text, _completedText, StringComparison.Ordinal))
            {
                _completionIndex = (_completionIndex + 1) % _completions.Length;
                ApplyCompletion(_completions[_completionIndex]);
                return;
            }

            _completions = _complete(text).ToArray();

            if (_completions.Length == 0)
                return;

            _completionIndex = 0;
            ApplyCompletion(_completions[0]);
        }

        private void ApplyCompletion(string path)
        {
            Text = path;
            CaretIndex = path.Length;
            _completedText = path;
        }

        private void ResetCompletion()
        {
            _completions = [];
            _completedText = null;
            _completionIndex = 0;
        }
    }

    private sealed record DirectoryEntry(
        string Name,
        string Path,
        bool IsParent,
        bool IsDrive,
        bool IsFile)
    {
        public static DirectoryEntry Folder(string path) =>
            new(Label(path), path, IsParent: false, IsDrive: false, IsFile: false);

        public static DirectoryEntry File(string path) =>
            new(Label(path), path, IsParent: false, IsDrive: false, IsFile: true);

        public static DirectoryEntry Parent(string path) =>
            new(Label(path), path, IsParent: true, IsDrive: false, IsFile: false);

        /// <summary>A drive listed at a root, which has no parent to rename or delete it within.</summary>
        public static DirectoryEntry Drive(string path) =>
            new(Label(path), path, IsParent: false, IsDrive: true, IsFile: false);

        /// <summary>
        /// A folder's own name, falling back to the full path for a root such as <c>C:\</c>, which has
        /// no name of its own. Naming the parent row after the folder it leads to is what says where
        /// walking up goes.
        /// </summary>
        private static string Label(string path)
        {
            string name = System.IO.Path.GetFileName(
                path.TrimEnd(
                    System.IO.Path.DirectorySeparatorChar,
                    System.IO.Path.AltDirectorySeparatorChar));

            return name.Length > 0 ? name : path;
        }
    }
}
