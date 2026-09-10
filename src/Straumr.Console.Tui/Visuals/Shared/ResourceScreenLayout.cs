using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// The resource-browser layout shared by the Workspaces, Auths and Secrets screens: a resource list
/// on the left and the selected resource's details on the right, divided by rules.
/// </summary>
/// <remarks>
/// The Requests screen has a third detail region below the two panes, so it composes
/// <see cref="Create"/> with its own sections rather than <see cref="TwoPaneSections"/>.
/// </remarks>
internal static class ResourceScreenLayout
{
    private static readonly Thickness PaneInset = new(1, 1, 1, 1);
    private static readonly Thickness BarInset = new(1, 0, 1, 0);

    /// <summary>
    /// Row offset where the list heading rule and the detail head rule meet the column divider. Both
    /// bars are three rows tall so the two rules land together.
    /// </summary>
    private const int HeadingRuleRow = 3;

    /// <summary>Row offset of the rule under the filter row. Only the list panel has a rule here.</summary>
    private const int FilterRuleRow = 5;

    /// <param name="listTitle">Heading of the list panel, for example <c>Workspaces</c>.</param>
    /// <param name="listCount">Total resource count, shown as a filled badge beside the heading.</param>
    /// <param name="filterHint">Placeholder shown on the filter row.</param>
    /// <param name="listContent">The list itself, or a loading, empty or error state.</param>
    /// <param name="detailHead">
    /// The selected resource's summary bar, or a message when nothing is selected. It must always
    /// return content: the bar's three rows are what align the two panels' rules.
    /// </param>
    /// <param name="detailSections">
    /// The rule closing <paramref name="detailHead"/> and everything below it, from
    /// <see cref="TwoPaneSections"/> or <see cref="EmptySections"/>.
    /// </param>
    public static Visual Create(
        string listTitle,
        Func<string> listCount,
        string filterHint,
        Func<Visual> listContent,
        Func<Visual> detailHead,
        Func<Visual> detailSections) =>
        new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(31) },
                new ColumnDefinition { Width = GridLength.Fixed(1) },
                new ColumnDefinition { Width = GridLength.Star(69) })
            .Rows(new RowDefinition { Height = GridLength.Star() })
            .Cell(BuildListPanel(listTitle, listCount, filterHint, listContent), 0, 0)
            .Cell(
                StraumrSurfaces.VerticalDivider(
                    (HeadingRuleRow, new Rune('┼')),
                    (FilterRuleRow, new Rune('┤'))),
                0,
                1)
            .Cell(BuildDetailPanel(detailHead, detailSections), 0, 2)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    /// <summary>Wraps pane content in the padding every detail pane uses.</summary>
    public static Visual Pane(Visual content) => StraumrSurfaces.Inset(content, PaneInset);

    /// <summary>Wraps a resource list in the styled scroll viewer every screen uses.</summary>
    public static Visual Scrollable(ResourceList list)
    {
        var scroller = new ScrollViewer(list, focusable: false)
        {
            HorizontalScrollEnabled = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        scroller.SetStyle(StraumrStyles.ListScrollViewer);
        return scroller;
    }

    /// <summary>Centres a loading, empty or error message in the region that owns it.</summary>
    public static Visual Message(Visual content) =>
        new Center(content)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    /// <summary>
    /// Two detail panes side by side, titled on the rule that closes the detail head.
    /// </summary>
    public static Visual TwoPaneSections(
        string leftTitle,
        Visual? left,
        string rightTitle,
        Visual? right) =>
        new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() })
            .Cell(
                PaneColumns()
                    .Cell(StraumrSurfaces.TitledDivider(leftTitle), 0, 0)
                    .Cell(StraumrSurfaces.VerticalDivider((0, new Rune('┬'))), 0, 1)
                    .Cell(StraumrSurfaces.TitledDivider(rightTitle), 0, 2),
                0,
                0)
            .Cell(
                PaneColumns()
                    .Cell(left ?? new Padder(), 0, 0)
                    .Cell(StraumrSurfaces.VerticalDivider(), 0, 1)
                    .Cell(right ?? new Padder(), 0, 2),
                1,
                0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    /// <summary>
    /// A plain closing rule and nothing below it, for when no resource is selected. Section titles
    /// over an empty region read as unfinished, so they are omitted rather than shown bare.
    /// </summary>
    public static Visual EmptySections() => StraumrSurfaces.HorizontalDivider();

    private static Visual BuildListPanel(
        string listTitle,
        Func<string> listCount,
        string filterHint,
        Func<Visual> listContent)
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
            new TextBlock(listTitle)
                .Style(() => panel.HasFocusWithin
                    ? StraumrStyles.AccentText
                    : StraumrStyles.AccentDimText),
            new TextBlock(() => $" {listCount()} ").Style(StraumrStyles.AccentChip));

        var filter = new TextBlock(filterHint)
            .Style(StraumrStyles.MutedText)
            .HorizontalAlignment(Align.Stretch);

        return panel
            .Cell(StraumrSurfaces.Inset(heading, PaneInset), 0, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 1, 0)
            .Cell(StraumrSurfaces.Inset(filter, BarInset), 2, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 3, 0)
            .Cell(new ComputedVisual(() => listContent())
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch), 4, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
    }

    private static Visual BuildDetailPanel(
        Func<Visual> detailHead,
        Func<Visual> detailSections) =>
        new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() })
            .Cell(StraumrSurfaces.Inset(
                new ComputedVisual(() => detailHead()).HorizontalAlignment(Align.Stretch),
                PaneInset), 0, 0)
            .Cell(new ComputedVisual(() => detailSections())
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch), 1, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    private static Grid PaneColumns() =>
        new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(48) },
                new ColumnDefinition { Width = GridLength.Fixed(1) },
                new ColumnDefinition { Width = GridLength.Star(52) })
            .Rows(new RowDefinition { Height = GridLength.Star() })
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
}
