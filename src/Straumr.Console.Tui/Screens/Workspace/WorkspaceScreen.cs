using System.Text;
using System.Text.Json;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Screens.Workspace;

public sealed class WorkspaceScreen
{
    private static readonly Thickness PaneInset = new(1, 1, 1, 1);
    private static readonly Thickness BarInset = new(1, 0, 1, 0);

    /// <summary>
    /// Row offset where the list heading rule and the detail summary rule meet the column divider.
    /// Both bars are three rows tall so the two rules land together.
    /// </summary>
    private const int HeadingRuleRow = 3;

    /// <summary>Row offset of the rule under the filter row. Only the list panel has a rule here.</summary>
    private const int FilterRuleRow = 5;

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
                new ColumnDefinition { Width = GridLength.Star(31) },
                new ColumnDefinition { Width = GridLength.Fixed(1) },
                new ColumnDefinition { Width = GridLength.Star(69) })
            .Rows(new RowDefinition { Height = GridLength.Star() })
            .Cell(BuildListPanel(), 0, 0)
            .Cell(
                StraumrSurfaces.VerticalDivider(
                    (HeadingRuleRow, new Rune('┼')),
                    (FilterRuleRow, new Rune('┤'))),
                0,
                1)
            .Cell(BuildDetailPanel(), 0, 2)
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

    private Visual BuildListPanel()
    {
        var panel = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() });

        Visual heading = StraumrSurfaces.Bar(
            new TextBlock("Workspaces")
                .Style(() => panel.HasFocusWithin
                    ? StraumrStyles.AccentText
                    : StraumrStyles.AccentDimText),
            new TextBlock(() => $" {_workspaceCount.Value} ").Style(StraumrStyles.AccentChip));

        var filter = new TextBlock("/ filter workspaces")
            .Style(StraumrStyles.MutedText)
            .HorizontalAlignment(Align.Stretch);

        panel
            .Cell(StraumrSurfaces.Inset(heading, PaneInset), 0, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 1, 0)
            .Cell(StraumrSurfaces.Inset(filter, BarInset), 2, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 3, 0)
            .Cell(new ComputedVisual(BuildListContent)
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch), 4, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        return panel;
    }

    private Visual BuildDetailPanel()
    {
        var panel = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() })
            .Cell(new ComputedVisual(BuildDetailSummary)
                .HorizontalAlignment(Align.Stretch), 0, 0)
            .Cell(new ComputedVisual(BuildPaneTitleRules)
                .HorizontalAlignment(Align.Stretch), 1, 0)
            .Cell(new ComputedVisual(BuildDetailPanes)
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch), 2, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        return panel;
    }

    private Visual? BuildListContent()
    {
        return _loadState.Value switch
        {
            WorkspaceLoadState.Loading => CenteredMessage(
                new HStack(
                        new Spinner(),
                        new TextBlock("Loading workspaces...").Style(StraumrStyles.MutedText))
                    .Spacing(1)),
            WorkspaceLoadState.Empty => CenteredMessage(
                new TextBlock("No workspaces found.").Style(StraumrStyles.MutedText)),
            WorkspaceLoadState.Error => CenteredMessage(
                new TextBlock(() => $"Failed to load workspaces: {_errorMessage.Value}")
                    .Style(StraumrStyles.MutedText)
                    .Wrap(true)),
            _ => BuildWorkspaceList()
        };
    }

    private Visual BuildWorkspaceList()
    {
        var list = new WorkspaceList(_items)
        {
            AutoFocus = true
        };
        list.BindSelectedIndex(_selectedIndex);

        var scroller = new ScrollViewer(list, focusable: false)
        {
            HorizontalScrollEnabled = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        scroller.SetStyle(StraumrStyles.ListScrollViewer);
        return scroller;
    }

    private Visual? BuildDetailSummary()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
        {
            return StraumrSurfaces.Inset(
                new TextBlock("No workspace selected.").Style(StraumrStyles.MutedText),
                PaneInset);
        }

        StraumrWorkspace workspace = item.Workspace;
        Visual summary = StraumrSurfaces.Bar(
            new HStack(
                    new TextBlock(workspace.Name).Style(StraumrStyles.PrimaryText),
                    new TextBlock($"last accessed {TimestampFormatting.Relative(workspace.LastAccessed)}")
                        .Style(StraumrStyles.MutedText))
                .Spacing(2),
            new TextBlock($" {item.ShortId} ").Style(StraumrStyles.TokenChip));

        return StraumrSurfaces.Inset(summary, PaneInset);
    }

    private Visual? BuildPaneTitleRules()
    {
        if (SelectedItem is null)
            return StraumrSurfaces.HorizontalDivider();

        return PaneColumns()
            .Cell(StraumrSurfaces.TitledDivider("Workspace"), 0, 0)
            .Cell(StraumrSurfaces.VerticalDivider((0, new Rune('┬'))), 0, 1)
            .Cell(StraumrSurfaces.TitledDivider("Requests"), 0, 2);
    }

    private static Grid PaneColumns() =>
        new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(48) },
                new ColumnDefinition { Width = GridLength.Fixed(1) },
                new ColumnDefinition { Width = GridLength.Star(52) })
            .Rows(new RowDefinition { Height = GridLength.Star() })
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    private Visual? BuildDetailPanes()
    {
        WorkspaceScreenItem? item = SelectedItem;
        if (item is null)
            return null;

        return PaneColumns()
            .Cell(BuildWorkspacePane(item), 0, 0)
            .Cell(StraumrSurfaces.VerticalDivider(), 0, 1)
            .Cell(new Padder(), 0, 2);
    }

    private static Visual BuildWorkspacePane(WorkspaceScreenItem item)
    {
        StraumrWorkspace workspace = item.Workspace;
        var fields = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(2)
            .Cell(FieldLabel("Path"), 0, 0)
            .Cell(new TextBlock(item.DisplayPath)
                .Style(StraumrStyles.PrimaryText)
                .Wrap(true)
                .Trimming(TextTrimming.EndEllipsis)
                .HorizontalAlignment(Align.Stretch), 0, 1)
            .Cell(FieldLabel("Requests"), 1, 0)
            .Cell(CountValue(workspace.Requests.Count), 1, 1)
            .Cell(FieldLabel("Auths"), 2, 0)
            .Cell(CountValue(workspace.Auths.Count), 2, 1)
            .Cell(FieldLabel("Modified"), 3, 0)
            .Cell(FieldValue(TimestampFormatting.Absolute(workspace.Modified)), 3, 1)
            .HorizontalAlignment(Align.Stretch);

        return StraumrSurfaces.Inset(fields, PaneInset);
    }

    private WorkspaceScreenItem? SelectedItem =>
        _selectedIndex.Value >= 0 &&
        _selectedIndex.Value < _items.Count
            ? _items[_selectedIndex.Value]
            : null;

    private static Visual CenteredMessage(Visual content) =>
        new Center(content)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    private static TextBlock FieldLabel(string text) =>
        new TextBlock(text).Style(StraumrStyles.MutedText);

    /// <summary>Counts share the list's semantics: a populated count is amber, an empty one is inert.</summary>
    private static TextBlock CountValue(int count) =>
        new TextBlock(count.ToString())
            .Style(count > 0 ? StraumrStyles.AmberText : StraumrStyles.MutedText)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    private static TextBlock FieldValue(string text) =>
        new TextBlock(text)
            .Style(StraumrStyles.PrimaryText)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    private enum WorkspaceLoadState
    {
        Loading,
        Loaded,
        Empty,
        Error
    }
}
