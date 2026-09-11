using System.Text.Json;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Infrastructure;
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

    public WorkspaceScreen(
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService,
        IStraumrRequestService requestService)
    {
        _optionsService = optionsService;
        _workspaceService = workspaceService;
        _requestService = requestService;

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
            _pendingActivationId = _visibleItems[index].Workspace.Id;
            _activationErrorMessage.Value = null;
        };
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
        _errorMessage.Value = null;

        try
        {
            await _optionsService.LoadAsync(cancellationToken);
            IReadOnlyList<StraumrWorkspace> workspaces =
                await _workspaceService.ListAsync(cancellationToken);

            Dictionary<Guid, StraumrWorkspaceEntry> entries = [];
            foreach (StraumrWorkspaceEntry entry in _optionsService.Options.Workspaces)
                entries.TryAdd(entry.Id, entry);

            _currentWorkspaceId.Value = _optionsService.Options.CurrentWorkspace?.Id;
            List<WorkspaceScreenItem> items = [];
            foreach (StraumrWorkspace workspace in workspaces)
            {
                if (entries.TryGetValue(workspace.Id, out StraumrWorkspaceEntry? entry))
                    items.Add(new WorkspaceScreenItem(workspace, entry));
            }

            _items = items;
            _workspaceCount.Value = items.Count;

            int currentIndex = items.FindIndex(
                item => item.Workspace.Id == _currentWorkspaceId.Value);
            Guid? preferredWorkspaceId = currentIndex >= 0
                ? items[currentIndex].Workspace.Id
                : items.FirstOrDefault()?.Workspace.Id;
            ApplyFilter(_filter.Text, preferredWorkspaceId);

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
            _ => _workspaceListView
        };
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
            "Details",
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
        await ActivateWorkspaceAsync(workspaceId, cancellationToken);
    }

    private async Task<TuiCommandResult> ActivateWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _workspaceService.ActivateAsync(workspaceId, cancellationToken);
            WorkspaceScreenItem? item = _items.Find(candidate => candidate.Workspace.Id == workspaceId);
            if (item is null)
                return TuiCommandResult.None;

            DateTimeOffset activatedAt = DateTimeOffset.UtcNow;
            item.Workspace.LastAccessed = activatedAt;
            _currentWorkspaceId.Value = workspaceId;
            _lastActivatedWorkspaceId.Value = workspaceId;
            _lastActivationTime.Value = activatedAt;
            ActiveWorkspaceName = item.Workspace.Name;
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
            ? await ActivateWorkspaceAsync(item.Workspace.Id, cancellationToken)
            : TuiCommandResult.Failed("no workspace selected");
    }

    private async Task<TuiCommandResult> RefreshAsync(
        string argument,
        CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
            return TuiCommandResult.Failed("usage: refresh");

        Guid? selected = SelectedItem?.Workspace.Id;
        _requestCache.Clear();
        _displayedRequestWorkspaceId = null;
        _requestLoadState.Value = RequestPreviewLoadState.Idle;
        await LoadAsync(cancellationToken);

        if (_loadState.Value == WorkspaceLoadState.Error)
            return TuiCommandResult.Failed($"refresh failed: {_errorMessage.Value}");

        int restored = selected is { } id
            ? _visibleItems.FindIndex(item => item.Workspace.Id == id)
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
                $"{name} matches {string.Join(", ", ambiguous.Select(item => item.Workspace.Name))}")
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
            item.Workspace.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return named.Count > 0
            ? named
            : _items.FindAll(item =>
                item.Workspace.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<string> WorkspaceNames() =>
        _items.Select(item => item.Workspace.Name);

    private void ApplyFilter(string text) => ApplyFilter(text, SelectedItem?.Workspace.Id);

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
            ? _visibleItems.FindIndex(item => item.Workspace.Id == id)
            : -1;
        _selectedIndex.Value = preferredIndex >= 0
            ? preferredIndex
            : _visibleItems.Count > 0 ? 0 : -1;
    }

    private void RefreshWorkspaceRows() =>
        _workspaceList.SetRows(
            _visibleItems.Select(item => ToRow(item, _currentWorkspaceId.Value)));

    private static bool MatchesFilter(WorkspaceScreenItem item, string query) =>
        item.Workspace.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase);

    private string FilterCount() =>
        _filterText.Value.Trim().Length == 0
            ? _workspaceCount.Value.ToString()
            : $"{_visibleWorkspaceCount.Value}/{_workspaceCount.Value}";

    private string NoMatchesMessage() =>
        $"No workspaces match {_filterText.Value.Trim()}.";

    private void ShowRequests(IReadOnlyList<StraumrRequest> requests)
    {
        _recentRequests.Value = requests;
        _requestLoadState.Value = requests.Count == 0
            ? RequestPreviewLoadState.Empty
            : RequestPreviewLoadState.Loaded;
    }

    private static ResourceRow ToRow(WorkspaceScreenItem item, Guid? currentWorkspaceId)
    {
        int requests = item.Workspace.Requests.Count;
        int auths = item.Workspace.Auths.Count;

        return new ResourceRow(
            item.Workspace.Name,
            $"{CountFormatting.Label(requests, "request")} · {CountFormatting.Label(auths, "auth")}",
            requests > 0 || auths > 0,
            item.DisplayDirectory,
            item.Workspace.Id == currentWorkspaceId);
    }

    private WorkspaceScreenItem? SelectedItem =>
        _selectedIndex.Value >= 0 &&
        _selectedIndex.Value < _visibleItems.Count
            ? _visibleItems[_selectedIndex.Value]
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
