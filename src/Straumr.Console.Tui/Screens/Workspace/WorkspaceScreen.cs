using System.Text.Json;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;

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

        Root = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(2) },
                new ColumnDefinition { Width = GridLength.Star(3) })
            .Rows(new RowDefinition { Height = GridLength.Star() })
            .ColumnGap(1)
            .Cell(BuildWorkspaceList(), 0, 0)
            .Cell(BuildWorkspaceDetails(), 0, 1)
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

    private Visual BuildWorkspaceList()
    {
        var group = new Group()
            .Padding(new Thickness(0))
            .Content(new Grid()
                .Columns(new ColumnDefinition { Width = GridLength.Star() })
                .Rows(
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star() })
                .Cell(
                    new Grid()
                        .Columns(
                            new ColumnDefinition { Width = GridLength.Star() },
                            new ColumnDefinition { Width = GridLength.Auto })
                        .Rows(new RowDefinition { Height = GridLength.Auto })
                        .Cell(new TextBlock("Workspaces")
                            .Style(StraumrStyles.AccentText), 0, 0)
                        .Cell(new TextBlock(() => _workspaceCount.Value.ToString())
                            .Style(StraumrStyles.MutedText), 0, 1)
                        .Margin(new Thickness(1, 1, 1, 1)),
                    0,
                    0)
                .Cell(CreateRule(), 1, 0)
                .Cell(
                    new ComputedVisual(BuildWorkspaceListContent)
                        .Margin(new Thickness(1))
                        .HorizontalAlignment(Align.Stretch)
                        .VerticalAlignment(Align.Stretch),
                    2,
                    0)
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch))
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        group.SetStyle(StraumrStyles.ListGroup);
        return group;
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
            _ => BuildWorkspaceItems()
        };
    }

    private Visual BuildWorkspaceItems()
    {
        var list = new WorkspaceList(_items)
        {
            AutoFocus = true
        };
        list.BindSelectedIndex(_selectedIndex);

        return new ScrollViewer(list, focusable: false)
        {
            HorizontalScrollEnabled = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private Visual? CreateWorkspaceDetails()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return BuildMessage(new TextBlock("No workspace selected."));

        StraumrWorkspace workspace = item.Workspace;
        var summary = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() },
                new ColumnDefinition { Width = GridLength.Auto })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Cell(new TextBlock(workspace.Name)
                .Style(StraumrStyles.PrimaryText), 0, 0)
            .Cell(new TextBlock($"last accessed {workspace.LastAccessed.LocalDateTime:yyyy-MM-dd HH:mm}")
                .Style(StraumrStyles.MutedText)
                .Margin(new Thickness(2, 0, 0, 0)), 0, 1)
            .Cell(new TextBlock(workspace.Id.ToString("N")[..8])
                .Style(StraumrStyles.MutedText), 0, 2)
            .HorizontalAlignment(Align.Stretch);

        var summaryPanel = CreateDetailPanel(summary);

        var workspaceDetails = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(2)
            .Cell(DetailLabel("Path"), 0, 0)
            .Cell(DetailValue(item.Entry.Path), 0, 1)
            .Cell(DetailLabel("Requests"), 1, 0)
            .Cell(DetailValue(workspace.Requests.Count.ToString()), 1, 1)
            .Cell(DetailLabel("Auths"), 2, 0)
            .Cell(DetailValue(workspace.Auths.Count.ToString()), 2, 1)
            .Cell(DetailLabel("Modified"), 3, 0)
            .Cell(DetailValue(workspace.Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm")), 3, 1)
            .HorizontalAlignment(Align.Stretch);

        Visual workspacePanel = CreateDetailPanel(
            new VStack(
                    new TextBlock("Workspace")
                        .Style(StraumrStyles.AccentText),
                    workspaceDetails)
                .Spacing(1)
                .HorizontalAlignment(Align.Stretch));

        Visual requestsPanel = CreateDetailPanel(
            new VStack(
                    new TextBlock("Requests")
                        .Style(StraumrStyles.AccentText))
                .HorizontalAlignment(Align.Stretch));

        var detailColumns = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star() },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Star() })
            .Cell(workspacePanel, 0, 0)
            .Cell(requestsPanel, 0, 1)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        return new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() })
            .Cell(summaryPanel, 0, 0)
            .Cell(detailColumns, 1, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
    }

    private WorkspaceScreenItem? SelectedItem =>
        _selectedIndex.Value >= 0 &&
        _selectedIndex.Value < _items.Count
            ? _items[_selectedIndex.Value]
            : null;

    private static Visual BuildMessage(Visual content) =>
        new Center(content)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    private static TextBlock DetailLabel(string text) =>
        new TextBlock(text).Style(StraumrStyles.MutedText);

    private static TextBlock DetailValue(string text) =>
        new TextBlock(text).Style(StraumrStyles.PrimaryText);

    private static Visual CreateDetailPanel(Visual content)
    {
        var panel = new Group()
            .Padding(new Thickness(1))
            .Content(content)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
        panel.SetStyle(StraumrStyles.DetailGroup);
        return panel;
    }

    private static Rule CreateRule()
    {
        var rule = new Rule();
        rule.SetStyle(RuleStyle.Default with
        {
            LineStyle = Style.None.WithForeground(StraumrStyles.Border)
        });
        return rule;
    }

    private enum WorkspaceLoadState
    {
        Loading,
        Loaded,
        Empty,
        Error
    }
}
