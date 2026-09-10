using System.Text.Json;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Workspace;

public sealed class WorkspaceScreen
{
    private readonly IStraumrOptionsService _optionsService;
    private readonly IStraumrWorkspaceService _workspaceService;
    private readonly IStraumrRequestService _requestService;
    private readonly State<WorkspaceLoadState> _loadState = new(WorkspaceLoadState.Loading);
    private readonly State<RequestPreviewLoadState> _requestLoadState = new(RequestPreviewLoadState.Idle);
    private readonly State<int> _workspaceCount = new(0);
    private readonly State<int> _selectedIndex = new(-1);
    private readonly State<string?> _errorMessage = new(null);
    private readonly State<string?> _requestErrorMessage = new(null);
    private readonly State<string?> _activationErrorMessage = new(null);
    private readonly State<Guid?> _lastActivatedWorkspaceId = new(null);
    private readonly State<DateTimeOffset?> _lastActivationTime = new(null);
    private readonly State<IReadOnlyList<StraumrRequest>> _recentRequests = new([]);
    private readonly Dictionary<Guid, IReadOnlyList<StraumrRequest>> _requestCache = [];
    private List<WorkspaceScreenItem> _items = [];
    private Guid? _displayedRequestWorkspaceId;
    private Guid? _pendingActivationId;

    public WorkspaceScreen(
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService,
        IStraumrRequestService requestService)
    {
        _optionsService = optionsService;
        _workspaceService = workspaceService;
        _requestService = requestService;

        Root = ResourceScreenLayout.Create(
            "Workspaces",
            () => _workspaceCount.Value.ToString(),
            "/ filter workspaces",
            BuildListContent,
            BuildDetailHead,
            BuildDetailSections);
    }

    public Visual Root { get; }

    public string? ActiveWorkspaceName { get; private set; }

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        await ActivatePendingWorkspaceAsync(cancellationToken);

        WorkspaceScreenItem? item = SelectedItem;
        if (item is null || item.Workspace.Id == _displayedRequestWorkspaceId)
            return;

        Guid workspaceId = item.Workspace.Id;
        _displayedRequestWorkspaceId = workspaceId;
        _requestErrorMessage.Value = null;

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

            if (SelectedItem?.Workspace.Id == workspaceId)
                ShowRequests(recent);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            if (SelectedItem?.Workspace.Id != workspaceId)
                return;

            _requestErrorMessage.Value = exception.Message;
            _requestLoadState.Value = RequestPreviewLoadState.Error;
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _optionsService.LoadAsync(cancellationToken);
            IReadOnlyList<StraumrWorkspace> workspaces =
                await _workspaceService.ListAsync(cancellationToken);

            Dictionary<Guid, StraumrWorkspaceEntry> entries = [];
            foreach (StraumrWorkspaceEntry entry in _optionsService.Options.Workspaces)
                entries.TryAdd(entry.Id, entry);

            Guid? currentWorkspaceId = _optionsService.Options.CurrentWorkspace?.Id;
            List<WorkspaceScreenItem> items = [];
            foreach (StraumrWorkspace workspace in workspaces)
            {
                if (entries.TryGetValue(workspace.Id, out StraumrWorkspaceEntry? entry))
                {
                    items.Add(new WorkspaceScreenItem(
                        workspace,
                        entry,
                        workspace.Id == currentWorkspaceId));
                }
            }

            _items = items;
            _workspaceCount.Value = items.Count;

            int currentIndex = items.FindIndex(item => item.IsCurrent);
            _selectedIndex.Value = currentIndex >= 0
                ? currentIndex
                : items.Count > 0 ? 0 : -1;

            ActiveWorkspaceName = currentIndex >= 0
                ? items[currentIndex].Workspace.Name
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

    private Visual BuildListContent()
    {
        return _loadState.Value switch
        {
            WorkspaceLoadState.Loading => ResourceScreenLayout.Message(
                new HStack(
                        new Spinner(),
                        new TextBlock("Loading workspaces...").Style(StraumrStyles.MutedText))
                    .Spacing(1)),
            WorkspaceLoadState.Empty => ResourceScreenLayout.Message(
                new TextBlock("No workspaces found.").Style(StraumrStyles.MutedText)),
            WorkspaceLoadState.Error => ResourceScreenLayout.Message(
                new TextBlock(() => $"Failed to load workspaces: {_errorMessage.Value}")
                    .Style(StraumrStyles.MutedText)
                    .Wrap(true)),
            _ => BuildWorkspaceList()
        };
    }

    private Visual BuildWorkspaceList()
    {
        var list = new ResourceList(_items.Select(ToRow))
        {
            AutoFocus = true
        };
        list.BindSelectedIndex(_selectedIndex);
        list.ItemActivated += index =>
        {
            _pendingActivationId = _items[index].Workspace.Id;
            _activationErrorMessage.Value = null;
        };
        return ResourceScreenLayout.Scrollable(list);
    }

    private Visual BuildDetailHead()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return new TextBlock("No workspace selected.").Style(StraumrStyles.MutedText);

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

        StraumrWorkspace workspace = item.Workspace;
        Visual fields = FieldList.Create(
            ("Path", FieldList.Wrapped(item.DisplayPath)),
            ("Requests", FieldList.Count(workspace.Requests.Count)),
            ("Auths", FieldList.Count(workspace.Auths.Count)),
            ("Modified", FieldList.Text(TimestampFormatting.Absolute(workspace.Modified))));

        return ResourceScreenLayout.TwoPaneSections(
            "Workspace",
            ResourceScreenLayout.Pane(fields),
            "Requests",
            ResourceScreenLayout.Pane(BuildRequestsPane()));
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

        try
        {
            await _workspaceService.ActivateAsync(workspaceId, cancellationToken);
            WorkspaceScreenItem? item = _items.Find(candidate => candidate.Workspace.Id == workspaceId);
            if (item is null)
                return;

            DateTimeOffset activatedAt = DateTimeOffset.UtcNow;
            item.Workspace.LastAccessed = activatedAt;
            _lastActivatedWorkspaceId.Value = workspaceId;
            _lastActivationTime.Value = activatedAt;
            ActiveWorkspaceName = item.Workspace.Name;
            _activationErrorMessage.Value = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StraumrException or IOException or UnauthorizedAccessException or JsonException)
        {
            _activationErrorMessage.Value = exception.Message;
        }
    }

    private void ShowRequests(IReadOnlyList<StraumrRequest> requests)
    {
        _recentRequests.Value = requests;
        _requestLoadState.Value = requests.Count == 0
            ? RequestPreviewLoadState.Empty
            : RequestPreviewLoadState.Loaded;
    }

    private static ResourceRow ToRow(WorkspaceScreenItem item)
    {
        int requests = item.Workspace.Requests.Count;
        int auths = item.Workspace.Auths.Count;

        return new ResourceRow(
            item.Workspace.Name,
            $"{CountFormatting.Label(requests, "request")} · {CountFormatting.Label(auths, "auth")}",
            requests > 0 || auths > 0,
            item.DisplayDirectory);
    }

    private WorkspaceScreenItem? SelectedItem =>
        _selectedIndex.Value >= 0 &&
        _selectedIndex.Value < _items.Count
            ? _items[_selectedIndex.Value]
            : null;

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
}
