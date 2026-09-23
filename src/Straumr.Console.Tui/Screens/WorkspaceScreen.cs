using System.Text.Json;
using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Console.Tui.Screens.Components.Workspace;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using ResourceList = Straumr.Console.Tui.Screens.Components.Shared.ResourceList;
using ScrollableContent = Straumr.Console.Tui.Screens.Components.Shared.ScrollableContent;

namespace Straumr.Console.Tui.Screens;

public sealed class WorkspaceScreen : ITuiScreen
{

    private const string InvalidJsonReason = "the workspace file is not valid JSON";

    private const string MismatchedIdReason =
        "the workspace file's ID no longer matches the registry";
    private readonly State<string?> _activationErrorMessage = new(null);
    private readonly State<Guid?> _currentWorkspaceId = new(null);
    private readonly State<string?> _errorMessage = new(null);
    private readonly ExternalEditorService _externalEditor;
    private readonly IStraumrFileService _fileService;
    private readonly ResourceFilter _filter;
    private readonly State<string> _filterText = new(string.Empty);
    private readonly State<Guid?> _lastActivatedWorkspaceId = new(null);
    private readonly State<DateTimeOffset?> _lastActivationTime = new(null);
    private readonly State<WorkspaceLoadState> _loadState = new(WorkspaceLoadState.Loading);
    private readonly State<IReadOnlyList<StraumrRequest>> _recentRequests = new([]);
    private readonly Dictionary<Guid, IReadOnlyList<StraumrRequest>> _requestCache = [];
    private readonly State<string?> _requestErrorMessage = new(null);
    private readonly State<RequestPreviewLoadState> _requestLoadState = new(RequestPreviewLoadState.Idle);
    private readonly IStraumrRequestService _requestService;
    private readonly State<int> _selectedIndex = new(-1);
    private readonly IStraumrSettingsService _settingsService;
    private readonly PaneSplits _splits = new();
    private readonly IStraumrStateService _stateService;
    private readonly State<int> _visibleWorkspaceCount = new(0);
    private readonly State<int> _workspaceCount = new(0);
    private readonly ResourceList _workspaceList;
    private readonly Visual _workspaceListView;
    private readonly IStraumrWorkspaceService _workspaceService;
    private Guid? _displayedRequestWorkspaceId;
    private List<WorkspaceScreenItemModel> _items = [];
    private Guid? _pendingActivationId;
    private WorkspaceCopySubmissionModel? _pendingCopy;
    private WorkspaceFormSubmissionModel? _pendingCreate;
    private Guid? _pendingDeleteId;
    private WorkspaceExportSubmissionModel? _pendingExport;
    private string? _pendingImportPath;
    private List<WorkspaceScreenItemModel> _visibleItems = [];

    public WorkspaceScreen(
        IStraumrStateService stateService,
        IStraumrSettingsService settingsService,
        IStraumrWorkspaceService workspaceService,
        IStraumrRequestService requestService,
        IStraumrFileService fileService,
        ExternalEditorService externalEditor)
    {
        _stateService = stateService;
        _settingsService = settingsService;
        _workspaceService = workspaceService;
        _requestService = requestService;
        _fileService = fileService;
        _externalEditor = externalEditor;

        _workspaceList = new ResourceList(
            [],
            ResourceScreenLayoutHelpers.Message(
                new TextBlock(() => NoMatchesMessage()).Style(StraumrStyleService.MutedText)));
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
            Gesture = TuiKeybindHelpers.Get("Workspace.Create"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => TuiKeybindHelpers.Run("Workspace.Create", ShowCreateDialog)
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Edit",
            LabelMarkup = "Edit",
            Gesture = TuiKeybindHelpers.Get("Workspace.Edit"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedItem is not null,
            Execute = _ => RequestEdit()
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Copy",
            LabelMarkup = "Copy",
            Gesture = TuiKeybindHelpers.Get("Workspace.Copy"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedItem is { IsCorrupt: false },
            Execute = _ => TuiKeybindHelpers.Run("Workspace.Copy", ShowCopyDialog)
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Delete",
            LabelMarkup = "Delete",
            Gesture = TuiKeybindHelpers.Get("Workspace.Delete"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedItem is not null,
            Execute = _ => ShowDeleteDialog()
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Import",
            LabelMarkup = "Import",
            Gesture = TuiKeybindHelpers.Get("Workspace.Import"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => ShowImportDialog()
        });
        _workspaceList.AddCommand(new Command
        {
            Id = "Workspace.Export",
            LabelMarkup = "Export",
            Gesture = TuiKeybindHelpers.Get("Workspace.Export"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedItem is { IsCorrupt: false },
            Execute = _ => ShowExportDialog()
        });
        _workspaceListView = ResourceScreenLayoutHelpers.Scrollable(_workspaceList);

        _filter = new ResourceFilter(
            "filter workspaces",
            ApplyFilter,
            () => _loadState.Value == WorkspaceLoadState.Loaded ? _workspaceList : null);

        Root = ResourceScreenLayoutHelpers.Create(
            "Workspaces",
            FilterCount,
            _filter,
            _splits,
            BuildListContent,
            BuildDetailHead,
            BuildDetailSections);

        PromptCommands =
        [
            new TuiCommandModel("select", SelectWorkspaceAsync)
            {
                Aliases = ["w"],
                ArgumentValues = WorkspaceIdentifiers
            },
            new TuiCommandModel("use", UseWorkspaceAsync)
            {
                Aliases = ["activate"],
                ArgumentValues = UsableWorkspaceIdentifiers,
                RunsInPlaceFromOtherScreens = true
            },
            new TuiCommandModel("create", CreateWorkspaceAsync) { Aliases = ["new"] },
            new TuiCommandModel("edit", EditWorkspaceAsync) { ArgumentValues = WorkspaceIdentifiers },
            new TuiCommandModel("copy", CopyWorkspaceAsync) { ArgumentValues = UsableWorkspaceIdentifiers },
            new TuiCommandModel("delete", DeleteWorkspaceAsync) { ArgumentValues = WorkspaceIdentifiers },
            new TuiCommandModel("import", ImportWorkspaceAsync),
            new TuiCommandModel("export", ExportWorkspaceAsync) { ArgumentValues = UsableWorkspaceIdentifiers },
            new TuiCommandModel("refresh", RefreshAsync)
        ];
    }

    private WorkspaceScreenItemModel? SelectedItem =>
        _selectedIndex.Value >= 0 &&
        _selectedIndex.Value < _visibleItems.Count
            ? _visibleItems[_selectedIndex.Value]
            : null;

    public Visual Root { get; }

    public TuiScreen Kind => TuiScreen.Workspaces;

    public Visual FocusTarget => _workspaceList;

    public IReadOnlyList<TuiCommandModel> PromptCommands { get; }

    public event Action<TuiCommandResultModel>? NotificationRequested;

    public event Action<TuiExternalActionModel>? ExternalActionRequested;

    public event Action? TransientScreenOpened
    {
        add { }
        remove { }
    }

    public event Action? TransientScreenClosed
    {
        add { }
        remove { }
    }

    public string? ActiveWorkspaceName { get; private set; }

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        await DeletePendingWorkspaceAsync(cancellationToken);
        await CreatePendingWorkspaceAsync(cancellationToken);
        await CopyPendingWorkspaceAsync(cancellationToken);
        await ImportPendingWorkspaceAsync(cancellationToken);
        await ExportPendingWorkspaceAsync(cancellationToken);
        await ActivatePendingWorkspaceAsync(cancellationToken);

        WorkspaceScreenItemModel? item = SelectedItem;
        if (item is null || item.Id == _displayedRequestWorkspaceId)
        {
            return;
        }

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
            {
                ShowRequests(recent);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            if (SelectedItem?.Id != workspaceId)
            {
                return;
            }

            _requestErrorMessage.Value = exception.Message;
            _requestLoadState.Value = RequestPreviewLoadState.Error;
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        _errorMessage.Value = null;

        try
        {
            await _stateService.LoadAsync(cancellationToken);
            _currentWorkspaceId.Value = _stateService.State.CurrentWorkspace?.Id;

            List<WorkspaceScreenItemModel> items = [];
            foreach (StraumrWorkspaceEntry entry in _stateService.State.Workspaces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(entry.Path))
                {
                    items.Add(await LoadItemAsync(entry, cancellationToken));
                }
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

    private async Task<WorkspaceScreenItemModel> LoadItemAsync(
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            StraumrWorkspace workspace = await _workspaceService.GetAsync(
                entry.Id,
                false,
                cancellationToken);
            return workspace.Id == entry.Id
                ? new WorkspaceScreenItemModel(workspace, entry)
                : new WorkspaceScreenItemModel(null, entry, MismatchedIdReason);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return new WorkspaceScreenItemModel(null, entry, InvalidJsonReason);
        }
        catch (StraumrException exception)
        {
            return new WorkspaceScreenItemModel(
                null,
                entry,
                exception.Reason == StraumrError.CorruptEntry
                    ? InvalidJsonReason
                    : exception.Message);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return new WorkspaceScreenItemModel(null, entry, exception.Message);
        }
    }

    private Visual BuildListContent()
    {
        return _loadState.Value switch
        {
            WorkspaceLoadState.Loading => ResourceScreenLayoutHelpers.Message(
                new HStack(
                        new Spinner(),
                        new TextBlock("Loading workspaces...").Style(StraumrStyleService.MutedText))
                    .Spacing(1)),
            WorkspaceLoadState.Empty => _workspaceListView,
            WorkspaceLoadState.Error => ResourceScreenLayoutHelpers.Message(
                new TextBlock(() => $"Failed to load workspaces: {_errorMessage.Value}")
                    .Style(StraumrStyleService.MutedText)
                    .Wrap(true)),
            _ => _workspaceListView
        };
    }

    private Visual BuildDetailHead()
    {
        WorkspaceScreenItemModel? item = SelectedItem;
        if (item is null)
        {
            return new TextBlock("No workspace selected.").Style(StraumrStyleService.MutedText);
        }

        if (item.IsCorrupt)
        {
            return StraumrSurfaceHelpers.Bar(
                new HStack(
                        new TextBlock(item.Name).Style(StraumrStyleService.RedText),
                        new TextBlock("cannot be read")
                            .Style(StraumrStyleService.RedText)
                            .Trimming(TextTrimming.EndEllipsis))
                    .Spacing(2),
                new TextBlock($" {item.ShortId} ").Style(StraumrStyleService.TokenChip));
        }

        StraumrWorkspace workspace = item.Workspace;
        DateTimeOffset lastAccessed = _lastActivatedWorkspaceId.Value == workspace.Id
            ? _lastActivationTime.Value ?? workspace.LastAccessed
            : workspace.LastAccessed;
        string status = _activationErrorMessage.Value is null
            ? $"last accessed {TimestampFormatting.Relative(lastAccessed)}"
            : $"activation failed: {_activationErrorMessage.Value}";
        return StraumrSurfaceHelpers.Bar(
            new HStack(
                    new TextBlock(workspace.Name).Style(StraumrStyleService.PrimaryText),
                    new TextBlock(status)
                        .Style(_activationErrorMessage.Value is null
                            ? StraumrStyleService.MutedText
                            : StraumrStyleService.RedText)
                        .Trimming(TextTrimming.EndEllipsis))
                .Spacing(2),
            new TextBlock($" {item.ShortId} ").Style(StraumrStyleService.TokenChip));
    }

    private Visual BuildDetailSections()
    {
        WorkspaceScreenItemModel? item = SelectedItem;
        if (item is null)
        {
            return ResourceScreenLayoutHelpers.EmptySections();
        }

        if (item.IsCorrupt)
        {
            return BuildCorruptSections(item.DisplayPath, item.Corruption);
        }

        StraumrWorkspace workspace = item.Workspace;
        Visual fields = FieldListHelpers.Create(
            ("Path", FieldListHelpers.Wrapped(item.DisplayPath)),
            ("Requests", FieldListHelpers.Count(workspace.Requests.Count)),
            ("Auths", FieldListHelpers.Count(workspace.Auths.Count)),
            ("Variables", FieldListHelpers.Count(workspace.Variables.Count)),
            ("Modified", FieldListHelpers.Text(TimestampFormatting.Absolute(workspace.Modified))));

        return ResourceScreenLayoutHelpers.TwoPaneSections(
            _splits,
            "Details",
            ResourceScreenLayoutHelpers.Pane(fields),
            "Requests",
            ResourceScreenLayoutHelpers.Pane(BuildRequestsPane()));
    }

    private Visual BuildCorruptSections(string displayPath, string problem)
    {
        Visual details = new VStack(
                FieldListHelpers.Create(
                    ("Path", FieldListHelpers.Wrapped(displayPath)),
                    ("Problem", FieldListHelpers.Problem(problem))),
                new TextBlock($"Press {TuiKeybindHelpers.Hint("Workspace.Edit")} to open the file in your editor and repair it.")
                    .Style(StraumrStyleService.MutedText)
                    .Wrap(true)
                    .HorizontalAlignment(Align.Stretch))
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        return ResourceScreenLayoutHelpers.TwoPaneSections(
            _splits,
            "Details",
            ResourceScreenLayoutHelpers.Pane(details),
            "Requests",
            ResourceScreenLayoutHelpers.Pane(ResourceScreenLayoutHelpers.Message(
                new TextBlock("Unavailable until the file is valid.")
                    .Style(StraumrStyleService.MutedText)
                    .Wrap(true))));
    }

    private Visual BuildRequestsPane() =>
        _requestLoadState.Value switch
        {
            RequestPreviewLoadState.Idle or RequestPreviewLoadState.Loading =>
                ResourceScreenLayoutHelpers.Message(
                    new HStack(
                            new Spinner(),
                            new TextBlock("Loading requests...").Style(StraumrStyleService.MutedText))
                        .Spacing(1)),
            RequestPreviewLoadState.Empty =>
                ResourceScreenLayoutHelpers.Message(
                    new TextBlock("No requests found.").Style(StraumrStyleService.MutedText)),
            RequestPreviewLoadState.Error =>
                ResourceScreenLayoutHelpers.Message(
                    new TextBlock(() => $"Failed to load requests: {_requestErrorMessage.Value}")
                        .Style(StraumrStyleService.MutedText)
                        .Wrap(true)),
            _ => BuildRecentRequests()
        };

    private Visual BuildRecentRequests()
    {
        IReadOnlyList<StraumrRequest> requests = _recentRequests.Value;
        VStack content = new VStack().HorizontalAlignment(Align.Stretch);

        for (int index = 0; index < requests.Count; index++)
        {
            if (index > 0)
            {
                content.Add(StraumrSurfaceHelpers.HorizontalDivider());
            }

            StraumrRequest request = requests[index];
            content.Add(new VStack(
                    new HStack(
                            new TextBlock(request.Method.Method)
                                .Style(HttpMethodFormatting.Style(request.Method)),
                            new TextBlock(request.Name)
                                .Style(StraumrStyleService.PrimaryText)
                                .Trimming(TextTrimming.EndEllipsis)
                                .HorizontalAlignment(Align.Stretch))
                        .Spacing(1)
                        .HorizontalAlignment(Align.Stretch),
                    new TextBlock("request · last used")
                        .Style(StraumrStyleService.MutedText),
                    new TextBlock(TimestampFormatting.Relative(request.LastAccessed))
                        .Style(StraumrStyleService.MutedText))
                .HorizontalAlignment(Align.Stretch));
        }

        return new ScrollableContent(content);
    }

    private async Task ActivatePendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingActivationId is not { } workspaceId)
        {
            return;
        }

        _pendingActivationId = null;
        await ActivateWorkspaceAsync(workspaceId, cancellationToken);
    }

    private void ShowDeleteDialog()
    {
        WorkspaceScreenItemModel? item = SelectedItem;
        if (item is null)
        {
            return;
        }

        new WorkspaceDeleteDialog(
                item.Name,
                () => _pendingDeleteId = item.Id)
            .Show();
    }

    private void RequestEdit()
    {
        if (SelectedItem is { } item)
        {
            NotifyIfFailed(Edit(item));
        }
    }

    private TuiCommandResultModel Edit(WorkspaceScreenItemModel item)
    {
        if (!_externalEditor.IsConfigured)
        {
            return TuiCommandResultModel.Failed("edit failed: no default editor is configured");
        }

        ExternalActionRequested?.Invoke(new TuiExternalActionModel(
            cancellationToken => EditWorkspaceFileAsync(item.Id, cancellationToken),
            _workspaceList));
        return TuiCommandResultModel.None;
    }

    private void NotifyIfFailed(TuiCommandResultModel result)
    {
        if (result.IsError)
        {
            NotificationRequested?.Invoke(result);
        }
    }

    private async Task<TuiCommandResultModel> EditWorkspaceFileAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        WorkspaceScreenItemModel? item = _items.Find(candidate => candidate.Id == workspaceId);
        if (item is null)
        {
            return TuiCommandResultModel.Failed("edit failed: the workspace is no longer listed");
        }

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
            return TuiCommandResultModel.Failed($"edit failed: {exception.Message}");
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException
                or JsonException)
        {
            return TuiCommandResultModel.Failed($"edit failed: {exception.Message}");
        }
    }

    private async Task<string> ReadForEditingAsync(
        WorkspaceScreenItemModel item,
        CancellationToken cancellationToken)
    {
        if (!item.IsCorrupt)
        {
            await _workspaceService.GetAsync(item.Id, false, cancellationToken);
        }

        return await File.ReadAllTextAsync(item.Entry.Path, cancellationToken);
    }

    private async Task<TuiCommandResultModel> SaveEditedWorkspaceAsync(
        WorkspaceScreenItemModel item,
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
            return TuiCommandResultModel.Failed($"saved {item.Name}, but {problem}; press {TuiKeybindHelpers.Hint("Workspace.Edit")} to fix it");
        }

        _fileService.CarryCommentsFrom(item.Entry.Path, edited);
        await _workspaceService.SaveAsync(workspace!, cancellationToken);
        await ReloadAndSelectAsync(item.Id, cancellationToken);
        return TuiCommandResultModel.Ok($"updated workspace {workspace!.Name}");
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
                _settingsService.DefaultWorkspacePath,
                'c',
                submission => _pendingCreate = submission)
            .Show();

    private void ShowCopyDialog()
    {
        WorkspaceScreenItemModel? item = SelectedItem;
        if (item is null)
        {
            return;
        }

        new WorkspaceFormDialog(
                "Copy workspace",
                "Copy",
                item.Name,
                item.ContainingDirectory ?? _settingsService.DefaultWorkspacePath,
                'y',
                submission => _pendingCopy = new WorkspaceCopySubmissionModel(
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
        WorkspaceScreenItemModel? item = SelectedItem;
        if (item is null)
        {
            return;
        }

        new FolderBrowserDialog(
                item.ContainingDirectory,
                Environment.CurrentDirectory,
                path => _pendingExport = new WorkspaceExportSubmissionModel(
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
        {
            return;
        }

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
                TuiCommandResultModel.Ok($"created workspace {workspace.Name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResultModel.Failed($"create failed: {exception.Message}"));
        }
    }

    private async Task CopyPendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingCopy is not { } pending)
        {
            return;
        }

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
                TuiCommandResultModel.Ok($"copied workspace {pending.Submission.Name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResultModel.Failed($"copy failed: {exception.Message}"));
        }
    }

    private async Task ImportPendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingImportPath is not { } path)
        {
            return;
        }

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
                TuiCommandResultModel.Ok($"imported workspace {name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResultModel.Failed($"import failed: {exception.Message}"));
        }
    }

    private async Task ExportPendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingExport is not { } pending)
        {
            return;
        }

        _pendingExport = null;
        try
        {
            string path = await _workspaceService.ExportAsync(
                pending.WorkspaceId,
                pending.OutputDirectory,
                cancellationToken);
            NotificationRequested?.Invoke(
                TuiCommandResultModel.Ok($"exported {pending.WorkspaceName} to {path}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResultModel.Failed($"export failed: {exception.Message}"));
        }
    }

    private async Task ReloadAndSelectAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (_filter.Text.Length > 0)
        {
            _filter.Clear();
        }

        await LoadAsync(cancellationToken);
        int index = _visibleItems.FindIndex(item => item.Id == workspaceId);
        if (index >= 0)
        {
            _selectedIndex.Value = index;
        }
    }

    private async Task DeletePendingWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_pendingDeleteId is not { } workspaceId)
        {
            return;
        }

        _pendingDeleteId = null;
        WorkspaceScreenItemModel? item = _items.Find(candidate => candidate.Id == workspaceId);
        if (item is null)
        {
            return;
        }

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
            {
                _selectedIndex.Value = Math.Clamp(selectedIndex, 0, _visibleItems.Count - 1);
            }

            NotificationRequested?.Invoke(
                TuiCommandResultModel.Ok($"deleted workspace {item.Name}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            NotificationRequested?.Invoke(
                TuiCommandResultModel.Failed($"delete failed: {exception.Message}"));
        }
    }

    private async Task<TuiCommandResultModel> ActivateWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        WorkspaceScreenItemModel? item = _items.Find(candidate => candidate.Id == workspaceId);
        if (item is null)
        {
            return TuiCommandResultModel.None;
        }

        if (item.IsCorrupt)
        {
            return TuiCommandResultModel.Failed($"cannot use {item.Name}: {item.Corruption}");
        }

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
            return TuiCommandResultModel.None;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            _activationErrorMessage.Value = exception.Message;
            return TuiCommandResultModel.Failed($"activation failed: {exception.Message}");
        }
    }

    private Task<TuiCommandResultModel> SelectWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken)
    {
        if (!TuiCommandArgumentHelpers.TryParseSingle(argument, out string name, out string? error))
        {
            return Task.FromResult(TuiCommandResultModel.Failed(error!));
        }

        return Task.FromResult(name.Length == 0
            ? TuiCommandResultModel.Failed("usage: select <name>")
            : SelectByName(name));
    }

    private async Task<TuiCommandResultModel> UseWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken)
    {
        if (!TuiCommandArgumentHelpers.TryParseSingle(argument, out string name, out string? error))
        {
            return TuiCommandResultModel.Failed(error!);
        }

        if (name.Length > 0)
        {
            TuiCommandResultModel selection = SelectByName(name);
            if (selection.IsError)
            {
                return selection;
            }
        }

        return SelectedItem is { } item
            ? await ActivateWorkspaceAsync(item.Id, cancellationToken)
            : TuiCommandResultModel.Failed("no workspace selected");
    }

    private async Task<TuiCommandResultModel> RefreshAsync(
        string argument,
        CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
        {
            return TuiCommandResultModel.Failed("usage: refresh");
        }

        Guid? selected = SelectedItem?.Id;
        _requestCache.Clear();
        _displayedRequestWorkspaceId = null;
        _requestLoadState.Value = RequestPreviewLoadState.Idle;
        await LoadAsync(cancellationToken);

        if (_loadState.Value == WorkspaceLoadState.Error)
        {
            return TuiCommandResultModel.Failed($"refresh failed: {_errorMessage.Value}");
        }

        int restored = selected is { } id
            ? _visibleItems.FindIndex(item => item.Id == id)
            : -1;
        if (restored >= 0)
        {
            _selectedIndex.Value = restored;
        }

        return TuiCommandResultModel.Ok($"reloaded {CountFormatting.Label(_items.Count, "workspace")}");
    }

    private TuiCommandResultModel OnSelected(
        string argument,
        Func<WorkspaceScreenItemModel, TuiCommandResultModel> action)
    {
        if (!TuiCommandArgumentHelpers.TryParseSingle(argument, out string name, out string? error))
        {
            return TuiCommandResultModel.Failed(error!);
        }

        if (name.Length > 0)
        {
            TuiCommandResultModel selection = SelectByName(name);
            if (selection.IsError)
            {
                return selection;
            }
        }

        return SelectedItem is { } item
            ? action(item)
            : TuiCommandResultModel.Failed("no workspace selected");
    }

    private Task<TuiCommandResultModel> CreateWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
        {
            return Task.FromResult(TuiCommandResultModel.Failed("usage: create"));
        }

        ShowCreateDialog();
        return Task.FromResult(TuiCommandResultModel.None);
    }

    private Task<TuiCommandResultModel> EditWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, Edit));

    private Task<TuiCommandResultModel> CopyWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item =>
        {
            if (item.IsCorrupt)
            {
                return TuiCommandResultModel.Failed($"cannot copy {item.Name}: the workspace cannot be read");
            }

            ShowCopyDialog();
            return TuiCommandResultModel.None;
        }));

    private Task<TuiCommandResultModel> DeleteWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, _ =>
        {
            ShowDeleteDialog();
            return TuiCommandResultModel.None;
        }));

    private Task<TuiCommandResultModel> ImportWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
        {
            return Task.FromResult(TuiCommandResultModel.Failed("usage: import"));
        }

        ShowImportDialog();
        return Task.FromResult(TuiCommandResultModel.None);
    }

    private Task<TuiCommandResultModel> ExportWorkspaceAsync(
        string argument,
        CancellationToken cancellationToken) =>
        Task.FromResult(OnSelected(argument, item =>
        {
            if (item.IsCorrupt)
            {
                return TuiCommandResultModel.Failed($"cannot export {item.Name}: the workspace cannot be read");
            }

            ShowExportDialog();
            return TuiCommandResultModel.None;
        }));

    private TuiCommandResultModel SelectByName(string name) =>
        MatchWorkspaces(name) switch
        {
            [] => TuiCommandResultModel.Failed($"no workspace matches {name}"),
            [WorkspaceScreenItemModel single] => Select(single),
            var ambiguous => TuiCommandResultModel.Failed(
                $"{name} matches {string.Join(", ", ambiguous.Select(item => item.Name))}")
        };

    private TuiCommandResultModel Select(WorkspaceScreenItemModel item)
    {
        if (_filter.Text.Length > 0)
        {
            _filter.Clear();
        }

        _selectedIndex.Value = _visibleItems.IndexOf(item);
        return TuiCommandResultModel.None;
    }

    private List<WorkspaceScreenItemModel> MatchWorkspaces(string name)
    {
        List<WorkspaceScreenItemModel> named = _items.FindAll(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            item.Id.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));

        return named.Count > 0
            ? named
            : _items.FindAll(item =>
                item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase) ||
                item.Id.ToString().StartsWith(name, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<string> WorkspaceIdentifiers() =>
        _items.SelectMany(item => new[] { item.Name, item.Id.ToString() });

    private IEnumerable<string> UsableWorkspaceIdentifiers() =>
        _items.Where(item => !item.IsCorrupt)
            .SelectMany(item => new[] { item.Name, item.Id.ToString() });

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

    private static bool MatchesFilter(WorkspaceScreenItemModel item, string query) =>
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

    private static ResourceRowModel ToRow(WorkspaceScreenItemModel item, Guid? currentWorkspaceId)
    {
        if (item.IsCorrupt)
        {
            return new ResourceRowModel(
                item.Name,
                "cannot be read",
                false,
                item.DisplayDirectory,
                item.Id == currentWorkspaceId,
                true);
        }

        int requests = item.Workspace.Requests.Count;
        int auths = item.Workspace.Auths.Count;

        return new ResourceRowModel(
            item.Name,
            $"{CountFormatting.Label(requests, "request")} · {CountFormatting.Label(auths, "auth")}",
            requests > 0 || auths > 0,
            item.DisplayDirectory,
            item.Id == currentWorkspaceId);
    }
}
