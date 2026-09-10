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
    private readonly State<WorkspaceLoadState> _loadState = new(WorkspaceLoadState.Loading);
    private readonly State<int> _workspaceCount = new(0);
    private readonly State<int> _selectedIndex = new(-1);
    private readonly State<string?> _errorMessage = new(null);
    private List<WorkspaceScreenItem> _items = [];

    public WorkspaceScreen(
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService)
    {
        _optionsService = optionsService;
        _workspaceService = workspaceService;

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
        return ResourceScreenLayout.Scrollable(list);
    }

    private Visual BuildDetailHead()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return new TextBlock("No workspace selected.").Style(StraumrStyles.MutedText);

        StraumrWorkspace workspace = item.Workspace;
        return StraumrSurfaces.Bar(
            new HStack(
                    new TextBlock(workspace.Name).Style(StraumrStyles.PrimaryText),
                    new TextBlock($"last accessed {TimestampFormatting.Relative(workspace.LastAccessed)}")
                        .Style(StraumrStyles.MutedText))
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
            null);
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
}
