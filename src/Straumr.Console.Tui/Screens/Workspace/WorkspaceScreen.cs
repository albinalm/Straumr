using System.Text.Json;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;
using XenoAtom.Terminal.UI.Templating;

namespace Straumr.Console.Tui.Screens.Workspace;

public sealed class WorkspaceScreen
{
    private static readonly TextBlockStyle MutedTextStyle =
        TextBlockStyle.Default with { Foreground = Colors.Gray };

    private readonly IStraumrOptionsService _optionsService;
    private readonly IStraumrWorkspaceService _workspaceService;
    private readonly State<WorkspaceLoadState> _loadState = new(WorkspaceLoadState.Loading);
    private readonly State<int> _workspaceCount = new(0);
    private readonly State<int> _selectedIndex = new(-1);
    private readonly State<string?> _errorMessage = new(null);
    private readonly ListBox<WorkspaceScreenItem> _workspaceList;

    public WorkspaceScreen(
        IStraumrOptionsService optionsService,
        IStraumrWorkspaceService workspaceService)
    {
        _optionsService = optionsService;
        _workspaceService = workspaceService;

        _workspaceList = new ListBox<WorkspaceScreenItem>()
            .ItemTemplate(new DataTemplate<WorkspaceScreenItem>(
                Display: CreateWorkspaceItem,
                Editor: null))
            .SelectedIndex(_selectedIndex)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        Root = new HSplitter(BuildWorkspaceList(), BuildWorkspaceDetails())
            .Ratio(0.38)
            .MinFirst(28)
            .MinSecond(36)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
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

            _workspaceList.Items.AddRange(items);
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

    private Visual BuildWorkspaceList()
    {
        return new Group()
            .TopLeftText(new TextBlock(() => $"Workspaces {_workspaceCount.Value}"))
            .Padding(new Thickness(1))
            .Content(new VStack(
                    new TextBlock("/ filter workspaces")
                        .Style(MutedTextStyle),
                    new ComputedVisual(BuildWorkspaceListContent))
                .Spacing(1)
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch))
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
    }

    private Visual BuildWorkspaceDetails() =>
        new ComputedVisual(CreateWorkspaceDetails)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    private Visual? BuildWorkspaceListContent()
    {
        return _loadState.Value switch
        {
            WorkspaceLoadState.Loading => BuildMessage(
                new HStack(new Spinner(), new TextBlock("Loading workspaces..."))
                    .Spacing(1)),
            WorkspaceLoadState.Empty => BuildMessage(
                new TextBlock("No workspaces found.")),
            WorkspaceLoadState.Error => BuildMessage(
                new TextBlock(() => $"Failed to load workspaces: {_errorMessage.Value}")),
            _ => _workspaceList
        };
    }

    private Visual? CreateWorkspaceDetails()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return BuildMessage(new TextBlock("No workspace selected."));

        StraumrWorkspace workspace = item.Workspace;
        var header = new Header
        {
            Left = new HStack(
                    new TextBlock(workspace.Name),
                    new TextBlock($"last accessed {workspace.LastAccessed.LocalDateTime:yyyy-MM-dd HH:mm}")
                        .Style(MutedTextStyle))
                .Spacing(2),
            Right = new TextBlock(workspace.Id.ToString("N")[..8])
                .Style(MutedTextStyle)
        };

        var details = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(2)
            .Cell("Path", 0, 0)
            .Cell(new TextBlock(item.Entry.Path), 0, 1)
            .Cell("Requests", 1, 0)
            .Cell(workspace.Requests.Count.ToString(), 1, 1)
            .Cell("Auths", 2, 0)
            .Cell(workspace.Auths.Count.ToString(), 2, 1)
            .Cell("Modified", 3, 0)
            .Cell(workspace.Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm"), 3, 1)
            .HorizontalAlignment(Align.Stretch);

        return new VStack(
                header,
                new Group("Workspace")
                    .Padding(new Thickness(1))
                    .Content(details)
                    .HorizontalAlignment(Align.Stretch)
                    .VerticalAlignment(Align.Stretch))
            .Spacing(0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
    }

    private WorkspaceScreenItem? SelectedItem =>
        _selectedIndex.Value >= 0 &&
        _selectedIndex.Value < _workspaceList.Items.Count
            ? _workspaceList.Items[_selectedIndex.Value]
            : null;

    private static Visual CreateWorkspaceItem(
        DataTemplateValue<WorkspaceScreenItem> value,
        in DataTemplateContext context)
    {
        WorkspaceScreenItem item = value.GetValue();
        StraumrWorkspace workspace = item.Workspace;

        return new VStack(
                new TextBlock(workspace.Name),
                new TextBlock($"{CountLabel(workspace.Requests.Count, "request")} · {CountLabel(workspace.Auths.Count, "auth")}")
                    .Style(MutedTextStyle),
                new TextBlock(item.Directory)
                    .Style(MutedTextStyle),
                new TextBlock(string.Empty))
            .HorizontalAlignment(Align.Stretch);
    }

    private static Visual BuildMessage(Visual content) =>
        new Center(content)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    private static string CountLabel(int count, string noun) =>
        count == 0
            ? $"no {noun}s"
            : $"{count} {noun}{(count == 1 ? string.Empty : "s")}";

    private enum WorkspaceLoadState
    {
        Loading,
        Loaded,
        Empty,
        Error
    }
}
