using System.Text;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;

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

    /// <summary>
    /// Top padding on list content so its first row lands on the same row as the first value in a
    /// detail pane, which takes that row from <see cref="PaneInset"/>.
    /// </summary>
    private static readonly Thickness ListContentInset = new(0, 1, 0, 0);

    /// <summary>
    /// Row offset where the rule closing each panel's bar meets the column divider. Both bars are
    /// three rows tall, so the two rules land together and every section title shares one line.
    /// </summary>
    private const int BarRuleRow = 3;

    /// <param name="listTitle">Heading of the list panel, for example <c>Workspaces</c>.</param>
    /// <param name="listCount">Resource count, shown as a recessed badge beside the filter.</param>
    /// <param name="filter">The inline filter shared by resource screens.</param>
    /// <param name="listContent">The list itself, or a loading, empty or error state.</param>
    /// <param name="detailHead">
    /// The selected resource's summary bar, or a message when nothing is selected. It must always
    /// return content: the bar's three rows are what align the two panels' rules.
    /// </param>
    /// <param name="detailSections">
    /// The rule closing <paramref name="detailHead"/> and everything below it, from
    /// <see cref="TwoPaneSections"/> or <see cref="EmptySections"/>.
    /// </param>
    /// <param name="splits">The screen's movable dividers, which outlive every rebuild of its content.</param>
    public static Visual Create(
        string listTitle,
        Func<string> listCount,
        ResourceFilter filter,
        PaneSplits splits,
        Func<Visual> listContent,
        Func<Visual> detailHead,
        Func<Visual> detailSections)
    {
        Visual detailPanel = BuildDetailPanel(detailHead, detailSections);

        var layout = new Grid()
            .Columns(
                splits.Panels.FirstColumn(),
                new ColumnDefinition { Width = GridLength.Fixed(1) },
                splits.Panels.SecondColumn())
            .Rows(new RowDefinition { Height = GridLength.Star() })
            .Cell(BuildListPanel(listTitle, listCount, filter, listContent), 0, 0)
            .Cell(
                StraumrSurfaces.VerticalDivider((BarRuleRow, new Rune('┼'))),
                0,
                1)
            .Cell(detailPanel, 0, 2)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        filter.AttachCommands(layout);
        AttachResizeCommands(layout, splits, filter, detailPanel);
        return layout;
    }

    /// <summary>
    /// <c>Ctrl</c> with a Vim direction moves a divider that way. Which divider depends on where
    /// focus is: in the list panel the horizontal keys move the panel divider, and in the detail
    /// panel they move the one between its two sections, so the keys always move the divider the
    /// focused region sits against. The vertical keys move the rule over a stacked pane, which only
    /// the detail panel has.
    /// </summary>
    /// <remarks>
    /// They are registered on the layout rather than globally so a dialog's focus chain never
    /// reaches them, and they stand down while the filter is being typed into: a terminal sends
    /// <c>Ctrl+H</c> as the byte <c>Backspace</c> arrives on, so a live command here would eat the
    /// filter's own deletion. <c>ConsumesGestureWhenUnavailable</c> is what lets the keystroke fall
    /// through to the editor instead of being swallowed.
    /// </remarks>
    private static void AttachResizeCommands(
        Visual layout,
        PaneSplits splits,
        ResourceFilter filter,
        Visual detailPanel)
    {
        PaneSplit Horizontal() => detailPanel.HasFocusWithin ? splits.Sections : splits.Panels;
        bool Editing() => filter.Root.HasFocusWithin;
        bool Stacked() => splits.HasStack && detailPanel.HasFocusWithin && !Editing();

        layout.AddCommand(ResizeCommand("Left", 'H', () => Horizontal().Move(-1), () => !Editing()));
        layout.AddCommand(ResizeCommand("Right", 'L', () => Horizontal().Move(1), () => !Editing(),
            CommandPresentation.CommandBar));
        layout.AddCommand(ResizeCommand("Up", 'K', () => splits.Stack.Move(-1), Stacked));
        layout.AddCommand(ResizeCommand("Down", 'J', () => splits.Stack.Move(1), Stacked));
    }

    /// <remarks>
    /// One of the four is presented and the rest are silent: the footer is a single row already
    /// carrying the screen's own actions, and four hints for one family of keys would crowd them out.
    /// </remarks>
    private static Command ResizeCommand(
        string direction,
        char key,
        Action execute,
        Func<bool> available,
        CommandPresentation presentation = CommandPresentation.None) =>
        new()
        {
            Id = $"ResourceScreen.Resize{direction}",
            LabelMarkup = "Resize panes",
            Gesture = new KeyGesture((char)(key & 0x1f), TerminalModifiers.Ctrl),
            Importance = CommandImportance.Tertiary,
            Presentation = presentation,
            CanExecute = _ => available(),
            IsVisible = _ => available(),
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => execute()
        };

    /// <summary>
    /// Wraps pane content in the padding every detail pane uses, inside a <see cref="FlexiblePane"/>
    /// so the content cannot outvote the weights its column was given.
    /// </summary>
    public static Visual Pane(Visual content) =>
        new FlexiblePane(StraumrSurfaces.Inset(content, PaneInset));

    /// <summary>Wraps scrollable pane content in the shared styled scroll viewer.</summary>
    public static Visual Scrollable(Visual content)
    {
        var scroller = new ScrollViewer(content, focusable: false)
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
        PaneSplits splits,
        string leftTitle,
        Visual? left,
        string rightTitle,
        Visual? right)
    {
        Visual leftPane = left ?? new Padder();
        Visual rightPane = right ?? new Padder();

        return PaneColumns(
                splits.Sections,
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() })
            .Cell(StraumrSurfaces.TitledDivider(leftTitle, leftPane.Owns), 0, 0)
            .Cell(StraumrSurfaces.VerticalDivider((0, new Rune('┬'))), 0, 1)
            .Cell(StraumrSurfaces.TitledDivider(rightTitle, rightPane.Owns), 0, 2)
            .Cell(leftPane, 1, 0)
            .Cell(StraumrSurfaces.VerticalDivider(), 1, 1)
            .Cell(rightPane, 1, 2)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
    }

    /// <summary>
    /// A plain closing rule and nothing below it, for when no resource is selected. Section titles
    /// over an empty region read as unfinished, so they are omitted rather than shown bare.
    /// </summary>
    public static Visual EmptySections() => StraumrSurfaces.HorizontalDivider();

    public static Visual StackedSections(
        PaneSplits splits,
        Visual overview,
        Visual divider,
        Visual preview)
    {
        splits.UseStack();
        return new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                splits.Stack.FirstRow(),
                new RowDefinition { Height = GridLength.Auto },
                splits.Stack.SecondRow())
            .Cell(overview, 0, 0)
            .Cell(divider, 1, 0)
            .Cell(preview, 2, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
    }

    /// <remarks>
    /// The panel mirrors the detail panel: a three-row bar, the rule closing it, then content. The list
    /// title sits on that rule beside the detail section titles, so every focusable region is titled on
    /// one line and the focus chip only ever travels along it. The filter and the count take the bar
    /// above, opposite each other exactly as the detail summary and its identifier badge do.
    /// </remarks>
    private static Visual BuildListPanel(
        string listTitle,
        Func<string> listCount,
        ResourceFilter filter,
        Func<Visual> listContent)
    {
        var panel = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() });

        Visual bar = StraumrSurfaces.Bar(
            filter.Root,
            new TextBlock(() => $" {listCount()} ").Style(StraumrStyles.TokenChip));

        return panel
            .Cell(StraumrSurfaces.Inset(bar, PaneInset), 0, 0)
            .Cell(StraumrSurfaces.TitledDivider(listTitle, panel.Owns), 1, 0)
            .Cell(
                StraumrSurfaces.Inset(
                    new ComputedVisual(() => listContent())
                        .HorizontalAlignment(Align.Stretch)
                        .VerticalAlignment(Align.Stretch),
                    ListContentInset),
                2,
                0)
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

    private static Grid PaneColumns(PaneSplit split, params RowDefinition[] rows) =>
        new Grid()
            .Columns(
                split.FirstColumn(),
                new ColumnDefinition { Width = GridLength.Fixed(1) },
                split.SecondColumn())
            .Rows(rows.Length == 0 ? [new RowDefinition { Height = GridLength.Star() }] : rows)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
}
