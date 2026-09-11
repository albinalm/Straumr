using System.Text.Json;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Workspace;

public sealed class WorkspaceScreen
{
    private readonly IStraumrOptionsService _optionsService;
    private readonly IStraumrWorkspaceService _workspaceService;
    private readonly IStraumrRequestService _requestService;
    private readonly ExternalEditor _externalEditor;
    private readonly State<WorkspaceLoadState> _loadState = new(WorkspaceLoadState.Loading);
    private readonly State<RequestPreviewLoadState> _requestLoadState = new(RequestPreviewLoadState.Idle);
    private readonly State<int> _workspaceCount = new(0);
    private readonly State<int> _visibleWorkspaceCount = new(0);
    private readonly State<int> _selectedIndex = new(-1);
    private readonly State<string> _filterText = new(string.Empty);
    private readonly State<string?> _errorMessage = new(null);
    private readonly State<string?> _requestErrorMessage = new(null);
    private readonly State<string?> _activationErrorMessage = new(null);
    private readonly State<Guid?> _currentWorkspaceId = new(null);
    private readonly State<Guid?> _lastActivatedWorkspaceId = new(null);
    private readonly State<DateTimeOffset?> _lastActivationTime = new(null);
    private readonly State<IReadOnlyList<StraumrRequest>> _recentRequests = new([]);
    private readonly Dictionary<Guid, IReadOnlyList<StraumrRequest>> _requestCache = [];
    private readonly ResourceFilter _filter;
    private readonly ResourceList _workspaceList;
    private readonly Visual _workspaceListView;
    private List<WorkspaceScreenItem> _items = [];
    private List<WorkspaceScreenItem> _visibleItems = [];
    private Guid? _displayedRequestWorkspaceId;
    private Guid? _pendingActivationId;
    private Guid? _pendingDeleteId;
    private WorkspaceFormSubmission? _pendingCreate;
    private WorkspaceCopySubmission? _pendingCopy;
    private string? _pendingImportPath;
    private WorkspaceExportSubmission? _pendingExport;

    public WorkspaceScreen(
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService,
        IStraumrRequestService requestService,
        ExternalEditor externalEditor)
    {
        _optionsService = optionsService;
        _workspaceService = workspaceService;
        _requestService = requestService;
        _externalEditor = externalEditor;

        _workspaceList = new ResourceList(
            [],
            ResourceScreenLayout.Message(
                new TextBlock(() => NoMatchesMessage()).Style(StraumrStyles.MutedText)))
        {
            AutoFocus = true
        };
        _workspaceList.BindSelectedIndex(_selectedIndex);
        _workspaceList.ItemActivated += index =>
        {
            _pendingActivationId = _visibleItems[index].Id;
            _activationErrorMessage.Value = null;
        };
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Create",
            LabelMarkup = "Create",
            Gesture = new KeyGesture('c'),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => ShowCreateDialog()
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Edit",
            LabelMarkup = "Edit",
            Gesture = new KeyGesture('e'),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedItem is not null,
            Execute = _ => RequestEdit()
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Copy",
            LabelMarkup = "Copy",
            Gesture = new KeyGesture('y'),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedItem is { IsCorrupt: false },
            Execute = _ => ShowCopyDialog()
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Delete",
            LabelMarkup = "Delete",
            Gesture = new KeyGesture('d'),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedItem is not null,
            Execute = _ => ShowDeleteDialog()
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Import",
            LabelMarkup = "Import",
            Gesture = new KeyGesture('i'),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => ShowImportDialog()
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Export",
            LabelMarkup = "Export",
            Gesture = new KeyGesture('x'),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedItem is { IsCorrupt: false },
            Execute = _ => ShowExportDialog()
        });
        _workspaceListView = ResourceScreenLayout.Scrollable(_workspaceList);

        _filter = new ResourceFilter(
            "filter workspaces",
            ApplyFilter,
            () => _loadState.Value == WorkspaceLoadState.Loaded ? _workspaceList : null);

        Root = ResourceScreenLayout.Create(
            "Workspaces",
            FilterCount,
            _filter,
            BuildListContent,
            BuildDetailHead,
            BuildDetailSections);

        PromptCommands =
        [
            new TuiCommand("workspace", SelectWorkspaceAsync)
            {
                ArgumentValues = WorkspaceNames
            },
            new TuiCommand("use", UseWorkspaceAsync)
            {
                ArgumentValues = WorkspaceNames
            },
            new TuiCommand("refresh", RefreshAsync)
        ];
    }

    public Visual Root { get; }

    public IReadOnlyList<TuiCommand> PromptCommands { get; }

    public event Action<TuiCommandResult>? NotificationRequested;

    public event Action<TuiExternalAction>? ExternalActionRequested;

    public string? ActiveWorkspaceName { get; private set; }

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        await DeletePendingWorkspaceAsync(cancellationToken);
        await CreatePendingWorkspaceAsync(cancellationToken);
        await CopyPendingWorkspaceAsync(cancellationToken);
        await ImportPendingWorkspaceAsync(cancellationToken);
        await ExportPendingWorkspaceAsync(cancellationToken);
        await ActivatePendingWorkspaceAsync(cancellationToken);

        WorkspaceScreenItem? item = SelectedItem;
        if (item is null || item.Id == _displayedRequestWorkspaceId)
            return;

        Guid workspaceId = item.Id;
        _displayedRequestWorkspaceId = workspaceId;
        _requestErrorMessage.Value = null;

        if (item.IsCorrupt)
        {
            _recentRequests.Value = [];
            _requestLoadState.Value = RequestPreviewLoadState.Idle;
            return;
        }

        if (_requestCache.TryGetValue(workspaceId, out IReadOnlyList<StraumrRequest>? cached))
        {
            ShowRequests(cached);
            return;
        }

        _recentRequests.Value = [];
        _requestLoadState.Value = RequestPreviewLoadState.Loading;

        try
        {
            IReadOnlyList<StraumrRequest> requests = await _requestService.ListAsync(
                item.Entry,
                cancellationToken);
            IReadOnlyList<StraumrRequest> recent = requests
                .OrderByDescending(request => request.LastAccessed)
                .ToArray();
            _requestCache[workspaceId] = recent;

            if (SelectedItem?.Id == workspaceId)
                ShowRequests(recent);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            if (SelectedItem?.Id != workspaceId)
                return;

            _requestErrorMessage.Value = exception.Message;
            _requestLoadState.Value = RequestPreviewLoadState.Error;
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        _errorMessage.Value = null;

        try
        {
            await _optionsService.LoadAsync(cancellationToken);
            _currentWorkspaceId.Value = _optionsService.Options.CurrentWorkspace?.Id;

            List<WorkspaceScreenItem> items = [];
            foreach (StraumrWorkspaceEntry entry in _optionsService.Options.Workspaces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // A registry entry whose folder is gone is not a workspace in trouble, it is one that
                // was removed from outside Straumr, which is what Core's own listing does with it too.
                if (File.Exists(entry.Path))
                    items.Add(await LoadItemAsync(entry, cancellationToken));
            }

            _items = items;
            _workspaceCount.Value = items.Count;

            int currentIndex = items.FindIndex(
                item => item.Id == _currentWorkspaceId.Value);
            Guid? preferredWorkspaceId = currentIndex >= 0
                ? items[currentIndex].Id
                : items.FirstOrDefault()?.Id;
            ApplyFilter(_filter.Text, preferredWorkspaceId);

            ActiveWorkspaceName = currentIndex >= 0
                ? items[currentIndex].Name
                : null;
            _loadState.Value = items.Count == 0
                ? WorkspaceLoadState.Empty
                : WorkspaceLoadState.Loaded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            _errorMessage.Value = exception.Message;
            _loadState.Value = WorkspaceLoadState.Error;
        }
    }

    /// <remarks>
    /// A workspace whose file cannot be read stays on the list as a corrupt item rather than being
    /// dropped from it. Dropping it hides the problem and the path to its file, and the file is often
    /// one edit away from being a workspace again.
    /// </remarks>
    private async Task<WorkspaceScreenItem> LoadItemAsync(
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            StraumrWorkspace workspace = await _workspaceService.GetAsync(
                entry.Id,
                updateLastAccessed: false,
                cancellationToken);
            return workspace.Id == entry.Id
                ? new WorkspaceScreenItem(workspace, entry)
                : new WorkspaceScreenItem(null, entry, MismatchedIdReason);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return new WorkspaceScreenItem(null, entry, InvalidJsonReason);
        }
        catch (StraumrException exception)
        {
            return new WorkspaceScreenItem(
                null,
                entry,
                exception.Reason == StraumrError.CorruptEntry
                    ? InvalidJsonReason
                    : exception.Message);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new WorkspaceScreenItem(null, entry, exception.Message);
        }
    }

    private Visual BuildListContent()
    {
        return _loadState.Value switch
        {
            WorkspaceLoadState.Loading => ResourceScreenLayout.Message(
                new HStack(
                        new Spinner(),
                        new TextBlock("Loading workspaces...").Style(StraumrStyles.MutedText))
                    .Spacing(1)),
            WorkspaceLoadState.Empty => _workspaceListView,
            WorkspaceLoadState.Error => ResourceScreenLayout.Message(
                new TextBlock(() => $"Failed to load workspaces: {_errorMessage.Value}")
                    .Style(StraumrStyles.MutedText)
                    .Wrap(true)),
            _ => _workspaceListView
        };
    }

    private Visual BuildDetailHead()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return new TextBlock("No workspace selected.").Style(StraumrStyles.MutedText);

        if (item.IsCorrupt)
        {
            return StraumrSurfaces.Bar(
                new HStack(
                        new TextBlock(item.Name).Style(StraumrStyles.RedText),
                        new TextBlock("cannot be read")
                            .Style(StraumrStyles.RedText)
                            .Trimming(TextTrimming.EndEllipsis))
                    .Spacing(2),
                new TextBlock($" {item.ShortId} ").Style(StraumrStyles.TokenChip));
        }

        StraumrWorkspace workspace = item.Workspace;
        DateTimeOffset lastAccessed = _lastActivatedWorkspaceId.Value == workspace.Id
            ? _lastActivationTime.Value ?? workspace.LastAccessed
            : workspace.LastAccessed;
        string status = _activationErrorMessage.Value is null
            ? $"last accessed {TimestampFormatting.Relative(lastAccessed)}"
            : $"activation failed: {_activationErrorMessage.Value}";
        return StraumrSurfaces.Bar(
            new HStack(
                    new TextBlock(workspace.Name).Style(StraumrStyles.PrimaryText),
                    new TextBlock(status)
                        .Style(_activationErrorMessage.Value is null
                            ? StraumrStyles.MutedText
                            : StraumrStyles.RedText)
                        .Trimming(TextTrimming.EndEllipsis))
                .Spacing(2),
            new TextBlock($" {item.ShortId} ").Style(StraumrStyles.TokenChip));
    }

    private Visual BuildDetailSections()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return ResourceScreenLayout.EmptySections();

        if (item.IsCorrupt)
            return BuildCorruptSections(item.DisplayPath, item.Corruption);

        StraumrWorkspace workspace = item.Workspace;
        Visual fields = FieldList.Create(
            ("Path", FieldList.Wrapped(item.DisplayPath)),
            ("Requests", FieldList.Count(workspace.Requests.Count)),
            ("Auths", FieldList.Count(workspace.Auths.Count)),
            ("Modified", FieldList.Text(TimestampFormatting.Absolute(workspace.Modified))));

        return ResourceScreenLayout.TwoPaneSections(
            "Details",
            ResourceScreenLayout.Pane(fields),
            "Requests",
            ResourceScreenLayout.Pane(BuildRequestsPane()));
    }

    /// <remarks>
    /// The detail pane is where the reason belongs in full: the list can only say that something is
    /// wrong, and the summary bar only has one trimmed line to say it in.
    /// </remarks>
    private static Visual BuildCorruptSections(string displayPath, string problem)
    {
        Visual details = new VStack(
                FieldList.Create(
                    ("Path", FieldList.Wrapped(displayPath)),
                    ("Problem", FieldList.Problem(problem))),
                new TextBlock("Press e to open the file in your editor and repair it.")
                    .Style(StraumrStyles.MutedText)
                    .Wrap(true)
                    .HorizontalAlignment(Align.Stretch))
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        return ResourceScreenLayout.TwoPaneSections(
            "Details",
            ResourceScreenLayout.Pane(details),
            "Requests",
            ResourceScreenLayout.Pane(ResourceScreenLayout.Message(
                new TextBlock("Unavailable until the file is valid.")
                    .Style(StraumrStyles.MutedText)
                    .Wrap(true))));
    }

    private Visual BuildRequestsPane() =>
        _requestLoadState.Value switch
        {
            RequestPreviewLoadState.Idle or RequestPreviewLoadState.Loading =>
                ResourceScreenLayout.Message(
                    new HStack(
                            new Spinner(),
                            new TextBlock("Loading requests...").Style(StraumrStyles.MutedText))
                        .Spacing(1)),
            RequestPreviewLoadState.Empty =>
                ResourceScreenLayout.Message(
                    new TextBlock("No requests found.").Style(StraumrStyles.MutedText)),
            RequestPreviewLoadState.Error =>
                ResourceScreenLayout.Message(
                    new TextBlock(() => $"Failed to load requests: {_requestErrorMessage.Value}")
                        .Style(StraumrStyles.MutedText)
                        .Wrap(true)),
            _ => BuildRecentRequests()
        };

    private Visual BuildRecentRequests()
    {
        IReadOnlyList<StraumrRequest> requests = _recentRequests.Value;
        var content = new VStack().HorizontalAlignment(Align.Stretch);

        for (int index = 0; index < requests.Count; index++)
        {
            if (index > 0)
                content.Add(StraumrSurfaces.HorizontalDivider());

            StraumrRequest request = requests[index];
            content.Add(new VStack(
                    new HStack(
                            new TextBlock(request.Method.Method)
                                .Style(HttpMethodFormatting.Style(request.Method)),
                            new TextBlock(request.Name)
                                .Style(StraumrStyles.PrimaryText)
                                .Trimming(TextTrimming.EndEllipsis)
                                .HorizontalAlignment(Align.Stretch))
                        .Spacing(1)
                        .HorizontalAlignment(Align.Stretch),
                    new TextBlock("request · last used")
                        .Style(StraumrStyles.MutedText),
                    new TextBlock(TimestampFormatting.Relative(request.LastAccessed))
                        .Style(StraumrStyles.MutedText))
                .HorizontalAlignment(Align.Stretch));
        }

        return new ScrollableContent(content);
    }

    private async Task ActivatePendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingActivationId is not { } workspaceId)
            return;

        _pendingActivationId = null;
        await ActivateWorkspaceAsync(workspaceId, cancellationToken);
    }

    private void ShowDeleteDialog()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return;

        new WorkspaceDeleteDialog(
            item.Name,
            () => _pendingDeleteId = item.Id)
            .Show();
    }

    private void RequestEdit()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return;

        if (!_externalEditor.IsConfigured)
        {
            NotificationRequested?.Invoke(
                TuiCommandResult.Failed("edit failed: no default editor is configured"));
            return;
        }

        ExternalActionRequested?.Invoke(new TuiExternalAction(
            cancellationToken => EditWorkspaceAsync(item.Id, cancellationToken),
            _workspaceList));
    }

    private async Task<TuiCommandResult> EditWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        WorkspaceScreenItem? item = _items.Find(candidate => candidate.Id == workspaceId);
        if (item is null)
            return TuiCommandResult.Failed("edit failed: the workspace is no longer listed");

        try
        {
            string edited = await _externalEditor.EditJsonAsync(
                await ReadForEditingAsync(item, cancellationToken),
                cancellationToken);
            return await SaveEditedWorkspaceAsync(item, edited, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ExternalEditorException exception)
        {
            return TuiCommandResult.Failed($"edit failed: {exception.Message}");
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException
                or JsonException)
        {
            return TuiCommandResult.Failed($"edit failed: {exception.Message}");
        }
    }

    /// <remarks>
    /// A workspace that still parses is re-read through Core so the editor opens on what is on disk
    /// rather than on what the screen loaded. One that does not parse has nothing for Core to return,
    /// and its file as it stands is exactly what has to be edited.
    /// </remarks>
    private async Task<string> ReadForEditingAsync(
        WorkspaceScreenItem item,
        CancellationToken cancellationToken)
    {
        if (item.IsCorrupt)
            return await File.ReadAllTextAsync(item.Entry.Path, cancellationToken);

        StraumrWorkspace workspace = await _workspaceService.GetAsync(
            item.Id,
            updateLastAccessed: false,
            cancellationToken);
        return JsonSerializer.Serialize(workspace, StraumrJsonContext.Default.StraumrWorkspace);
    }

    /// <remarks>
    /// An edit Core cannot accept is still the developer's work, so it is written to the workspace file
    /// as it stands and the workspace is listed as corrupt until it is repaired. Reporting the mistake
    /// and discarding the text costs the edit and offers nothing back; keeping it means pressing
    /// <c>e</c> again reopens the very text that needs fixing.
    /// </remarks>
    private async Task<TuiCommandResult> SaveEditedWorkspaceAsync(
        WorkspaceScreenItem item,
        string edited,
        CancellationToken cancellationToken)
    {
        _requestCache.Remove(item.Id);
        _displayedRequestWorkspaceId = null;
        _requestLoadState.Value = RequestPreviewLoadState.Idle;

        StraumrWorkspace? workspace = TryReadWorkspace(edited);
        string? problem = workspace is null
            ? InvalidJsonReason
            : workspace.Id != item.Id
                ? MismatchedIdReason
                : null;

        if (problem is not null)
        {
            await File.WriteAllTextAsync(item.Entry.Path, edited, cancellationToken);
            await ReloadAndSelectAsync(item.Id, cancellationToken);
            return TuiCommandResult.Failed($"saved {item.Name}, but {problem}; press e to fix it");
        }

        await _workspaceService.SaveAsync(workspace!, cancellationToken);
        await ReloadAndSelectAsync(item.Id, cancellationToken);
        return TuiCommandResult.Ok($"updated workspace {workspace!.Name}");
    }

    private static StraumrWorkspace? TryReadWorkspace(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, StraumrJsonContext.Default.StraumrWorkspace);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void ShowCreateDialog() =>
        new WorkspaceFormDialog(
            "Create workspace",
            "Create",
            null,
            _optionsService.Options.DefaultWorkspacePath,
            'c',
            submission => _pendingCreate = submission)
        .Show();

    private void ShowCopyDialog()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return;

        // A copy belongs beside what it was copied from far more often than in whatever the global
        // default currently points at, which is also the setting most likely to have gone stale.
        new WorkspaceFormDialog(
            "Copy workspace",
            "Copy",
            item.Name,
            item.ContainingDirectory ?? _optionsService.Options.DefaultWorkspacePath,
            'y',
            submission => _pendingCopy = new WorkspaceCopySubmission(
                item.Id,
                submission))
            .Show();
    }

    private void ShowImportDialog()
    {
        new FileBrowserDialog(
            null,
            Environment.CurrentDirectory,
            path => _pendingImportPath = path,
            file => Path.GetExtension(file)
                .Equals(".straumrpak", StringComparison.OrdinalIgnoreCase),
            "Import workspace",
            "Import")
        .Show();
    }

    private void ShowExportDialog()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return;

        new FolderBrowserDialog(
            item.ContainingDirectory,
            Environment.CurrentDirectory,
            path => _pendingExport = new WorkspaceExportSubmission(
                item.Id,
                item.Name,
                path),
            $"Export {item.Name}",
            "Export here")
        .Show();
    }

    private async Task CreatePendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingCreate is not { } submission)
            return;

        _pendingCreate = null;
        try
        {
            var workspace = new StraumrWorkspace { Name = submission.Name };
            await _workspaceService.CreateAsync(
                workspace,
                submission.OutputDirectory,
                cancellationToken);
            await ReloadAndSelectAsync(workspace.Id, cancellationToken);
            NotificationRequested?.Invoke(
                TuiCommandResult.Ok($"created workspace {workspace.Name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResult.Failed($"create failed: {exception.Message}"));
        }
    }

    private async Task CopyPendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingCopy is not { } pending)
            return;

        _pendingCopy = null;
        try
        {
            StraumrWorkspaceEntry entry = await _workspaceService.CopyAsync(
                pending.SourceId,
                pending.Submission.Name,
                pending.Submission.OutputDirectory,
                cancellationToken);
            await ReloadAndSelectAsync(entry.Id, cancellationToken);
            NotificationRequested?.Invoke(
                TuiCommandResult.Ok($"copied workspace {pending.Submission.Name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResult.Failed($"copy failed: {exception.Message}"));
        }
    }

    private async Task ImportPendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingImportPath is not { } path)
            return;

        _pendingImportPath = null;
        try
        {
            StraumrWorkspaceEntry entry = await _workspaceService.ImportAsync(
                path,
                cancellationToken);
            await ReloadAndSelectAsync(entry.Id, cancellationToken);
            string name = _items
                .Find(item => item.Id == entry.Id)?
                .Name ?? Path.GetFileNameWithoutExtension(path);
            NotificationRequested?.Invoke(
                TuiCommandResult.Ok($"imported workspace {name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResult.Failed($"import failed: {exception.Message}"));
        }
    }

    private async Task ExportPendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingExport is not { } pending)
            return;

        _pendingExport = null;
        try
        {
            string path = await _workspaceService.ExportAsync(
                pending.WorkspaceId,
                pending.OutputDirectory,
                cancellationToken);
            NotificationRequested?.Invoke(
                TuiCommandResult.Ok($"exported {pending.WorkspaceName} to {path}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResult.Failed($"export failed: {exception.Message}"));
        }
    }

    private async Task ReloadAndSelectAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (_filter.Text.Length > 0)
            _filter.Clear();

        await LoadAsync(cancellationToken);
        int index = _visibleItems.FindIndex(item => item.Id == workspaceId);
        if (index >= 0)
            _selectedIndex.Value = index;
    }

    private async Task DeletePendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingDeleteId is not { } workspaceId)
            return;

        _pendingDeleteId = null;
        WorkspaceScreenItem? item = _items.Find(candidate => candidate.Id == workspaceId);
        if (item is null)
            return;

        int selectedIndex = _selectedIndex.Value;

        try
        {
            await _workspaceService.DeleteAsync(workspaceId, cancellationToken);
            _requestCache.Remove(workspaceId);
            _displayedRequestWorkspaceId = null;
            _recentRequests.Value = [];
            _requestLoadState.Value = RequestPreviewLoadState.Idle;
            await LoadAsync(cancellationToken);

            if (_visibleItems.Count > 0)
                _selectedIndex.Value = Math.Clamp(selectedIndex, 0, _visibleItems.Count - 1);

            NotificationRequested?.Invoke(
                TuiCommandResult.Ok($"deleted workspace {item.Name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResult.Failed($"delete failed: {exception.Message}"));
        }
    }

    private async Task<TuiCommandResult> ActivateWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        WorkspaceScreenItem? item = _items.Find(candidate => candidate.Id == workspaceId);
        if (item is null)
            return TuiCommandResult.None;

        if (item.IsCorrupt)
            return TuiCommandResult.Failed($"cannot use {item.Name}: {item.Corruption}");

        try
        {
            await _workspaceService.ActivateAsync(workspaceId, cancellationToken);

            DateTimeOffset activatedAt = DateTimeOffset.UtcNow;
            item.Workspace!.LastAccessed = activatedAt;
            _currentWorkspaceId.Value = workspaceId;
            _lastActivatedWorkspaceId.Value = workspaceId;
            _lastActivationTime.Value = activatedAt;
            ActiveWorkspaceName = item.Name;
            _activationErrorMessage.Value = null;
            RefreshWorkspaceRows();
            return TuiCommandResult.None;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            _activationErrorMessage.Value = exception.Message;
            return TuiCommandResult.Failed($"activation failed: {exception.Message}");
        }
    }

    private Task<TuiCommandResult> SelectWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken) =>
        Task.FromResult(argument.Length == 0
            ? TuiCommandResult.Failed("usage: workspace <name>")
            : SelectByName(argument));

    private async Task<TuiCommandResult> UseWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
        {
            TuiCommandResult selection = SelectByName(argument);
            if (selection.IsError)
                return selection;
        }

        return SelectedItem is { } item
            ? await ActivateWorkspaceAsync(item.Id, cancellationToken)
            : TuiCommandResult.Failed("no workspace selected");
    }

    private async Task<TuiCommandResult> RefreshAsync(
        string argument,
        CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
            return TuiCommandResult.Failed("usage: refresh");

        Guid? selected = SelectedItem?.Id;
        _requestCache.Clear();
        _displayedRequestWorkspaceId = null;
        _requestLoadState.Value = RequestPreviewLoadState.Idle;
        await LoadAsync(cancellationToken);

        if (_loadState.Value == WorkspaceLoadState.Error)
            return TuiCommandResult.Failed($"refresh failed: {_errorMessage.Value}");

        int restored = selected is { } id
            ? _visibleItems.FindIndex(item => item.Id == id)
            : -1;
        if (restored >= 0)
            _selectedIndex.Value = restored;

        return TuiCommandResult.Ok($"reloaded {CountFormatting.Label(_items.Count, "workspace")}");
    }

    private TuiCommandResult SelectByName(string name) =>
        MatchWorkspaces(name) switch
        {
            [] => TuiCommandResult.Failed($"no workspace matches {name}"),
            [WorkspaceScreenItem single] => Select(single),
            var ambiguous => TuiCommandResult.Failed(
                $"{name} matches {string.Join(", ", ambiguous.Select(item => item.Name))}")
        };

    private TuiCommandResult Select(WorkspaceScreenItem item)
    {
        if (_filter.Text.Length > 0)
            _filter.Clear();

        _selectedIndex.Value = _visibleItems.IndexOf(item);
        return TuiCommandResult.None;
    }

    private List<WorkspaceScreenItem> MatchWorkspaces(string name)
    {
        List<WorkspaceScreenItem> named = _items.FindAll(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return named.Count > 0
            ? named
            : _items.FindAll(item =>
                item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<string> WorkspaceNames() =>
        _items.Select(item => item.Name);

    private void ApplyFilter(string text) => ApplyFilter(text, SelectedItem?.Id);

    private void ApplyFilter(string text, Guid? preferredWorkspaceId)
    {
        _filterText.Value = text;
        string query = text.Trim();
        _visibleItems = query.Length == 0
            ? [.. _items]
            : _items.Where(item => MatchesFilter(item, query)).ToList();
        _visibleWorkspaceCount.Value = _visibleItems.Count;

        RefreshWorkspaceRows();

        int preferredIndex = preferredWorkspaceId is { } id
            ? _visibleItems.FindIndex(item => item.Id == id)
            : -1;
        _selectedIndex.Value = preferredIndex >= 0
            ? preferredIndex
            : _visibleItems.Count > 0 ? 0 : -1;
    }

    private void RefreshWorkspaceRows() =>
        _workspaceList.SetRows(
            _visibleItems.Select(item => ToRow(item, _currentWorkspaceId.Value)));

    private static bool MatchesFilter(WorkspaceScreenItem item, string query) =>
        item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase);

    private string FilterCount() =>
        _filterText.Value.Trim().Length == 0
            ? _workspaceCount.Value.ToString()
            : $"{_visibleWorkspaceCount.Value}/{_workspaceCount.Value}";

    private string NoMatchesMessage() =>
        _filterText.Value.Trim().Length == 0
            ? "No workspaces found."
            : $"No workspaces match {_filterText.Value.Trim()}.";

    private void ShowRequests(IReadOnlyList<StraumrRequest> requests)
    {
        _recentRequests.Value = requests;
        _requestLoadState.Value = requests.Count == 0
            ? RequestPreviewLoadState.Empty
            : RequestPreviewLoadState.Loaded;
    }

    private static ResourceRow ToRow(WorkspaceScreenItem item, Guid? currentWorkspaceId)
    {
        if (item.IsCorrupt)
        {
            return new ResourceRow(
                item.Name,
                "cannot be read",
                HasContent: false,
                item.DisplayDirectory,
                item.Id == currentWorkspaceId,
                IsBroken: true);
        }

        int requests = item.Workspace.Requests.Count;
        int auths = item.Workspace.Auths.Count;

        return new ResourceRow(
            item.Name,
            $"{CountFormatting.Label(requests, "request")} · {CountFormatting.Label(auths, "auth")}",
            requests > 0 || auths > 0,
            item.DisplayDirectory,
            item.Id == currentWorkspaceId);
    }

    private WorkspaceScreenItem? SelectedItem =>
        _selectedIndex.Value >= 0 &&
        _selectedIndex.Value < _visibleItems.Count
            ? _visibleItems[_selectedIndex.Value]
            : null;

    private const string InvalidJsonReason = "the workspace file is not valid JSON";

    private const string MismatchedIdReason =
        "the workspace file's ID no longer matches the registry";

    private enum WorkspaceLoadState
    {
        Loading,
        Loaded,
        Empty,
        Error
    }

    private enum RequestPreviewLoadState
    {
        Idle,
        Loading,
        Loaded,
        Empty,
        Error
    }

    private sealed record WorkspaceCopySubmission(
        Guid SourceId,
        WorkspaceFormSubmission Submission);

    private sealed record WorkspaceExportSubmission(
        Guid WorkspaceId,
        string WorkspaceName,
        string OutputDirectory);
}
