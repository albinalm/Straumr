using System.Text;
using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Helpers;

internal static class ResourceScreenLayoutHelpers
{

    private const int BarRuleRow = 3;
    public static readonly Thickness PaneInset = new(1, 1, 1, 1);

    private static readonly Thickness ListContentInset = new(0, 1, 0, 0);

    public static Visual Create(
        string listTitle,
        Func<string> listCount,
        ResourceFilter filter,
        PaneSplits splits,
        Func<Visual> listContent,
        Func<Visual> detailHead,
        Func<Visual> detailSections)
    {
        Visual listPanel = new FlexiblePane(BuildListPanel(listTitle, listCount, filter, listContent));
        Visual detailPanel = new FlexiblePane(BuildDetailPanel(detailHead, detailSections));

        Grid layout = new Grid()
            .Columns(
                splits.Panels.FirstColumn(),
                new ColumnDefinition { Width = GridLength.Fixed(1) },
                splits.Panels.SecondColumn())
            .Rows(new RowDefinition { Height = GridLength.Star() })
            .Cell(listPanel, 0, 0)
            .Cell(
                StraumrSurfaceHelpers.VerticalDivider((BarRuleRow, new Rune('┼'))),
                0,
                1)
            .Cell(detailPanel, 0, 2)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        filter.AttachCommands(layout);
        AttachResizeCommands(layout, splits, filter, detailPanel);
        return layout;
    }

    private static void AttachResizeCommands(
        Visual layout,
        PaneSplits splits,
        ResourceFilter filter,
        Visual detailPanel)
    {
        PaneSplit Horizontal() => detailPanel.HasFocusWithin ? splits.Sections : splits.Panels;
        bool Editing() => filter.Root.Owns();
        bool Stacked() => splits.HasStack && detailPanel.HasFocusWithin && !Editing();

        layout.AddCommand(ResizeCommand("Left", () => Horizontal().Move(-1), () => !Editing() && !Stacked(),
            CommandPresentation.CommandBar, TuiKeybindHelpers.CombinedLabel("Resize", "ResourceScreen.ResizeRight")));
        layout.AddCommand(ResizeCommand("LeftStacked", () => Horizontal().Move(-1), Stacked,
            CommandPresentation.CommandBar,
            TuiKeybindHelpers.CombinedLabel("Resize", "ResourceScreen.ResizeDown", "ResourceScreen.ResizeUp", "ResourceScreen.ResizeRight")));
        layout.AddCommand(ResizeCommand("Right", () => Horizontal().Move(1), () => !Editing()));
        layout.AddCommand(ResizeCommand("Up", () => splits.Stack.Move(-1), Stacked));
        layout.AddCommand(ResizeCommand("Down", () => splits.Stack.Move(1), Stacked));
    }

    private static Command ResizeCommand(
        string direction,
        Action execute,
        Func<bool> available,
        CommandPresentation presentation = CommandPresentation.None,
        string label = "Resize") =>
        new()
        {
            Id = $"ResourceScreen.Resize{direction}",
            LabelMarkup = label,
            Gesture = TuiKeybindHelpers.Get($"ResourceScreen.Resize{direction}"),
            Importance = CommandImportance.Tertiary,
            Presentation = presentation == CommandPresentation.None
                ? TuiKeybindHelpers.SecondaryPresentation("ResourceScreen.ResizeLeft") : presentation,
            CanExecute = _ => available(),
            IsVisible = _ => available(),
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => execute()
        };

    public static Visual Pane(Visual content) =>
        new FlexiblePane(StraumrSurfaceHelpers.Inset(content, PaneInset));

    public static Visual Scrollable(Visual content)
    {
        var scroller = new ScrollViewer(content, false)
        {
            HorizontalScrollEnabled = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        scroller.SetStyle(StraumrStyleService.ListScrollViewer);
        return scroller;
    }

    public static Visual Message(Visual content) =>
        new Center(content)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    public static Visual TwoPaneSections(
        PaneSplits splits,
        string leftTitle,
        Visual? left,
        string rightTitle,
        Visual? right,
        Func<bool>? leftFocused = null)
    {
        Visual leftPane = left ?? new Padder();
        Visual rightPane = right ?? new Padder();

        return PaneColumns(
                splits.Sections,
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() })
            .Cell(StraumrSurfaceHelpers.TitledDivider(leftTitle, leftFocused ?? leftPane.Owns), 0, 0)
            .Cell(StraumrSurfaceHelpers.VerticalDivider((0, new Rune('┬'))), 0, 1)
            .Cell(StraumrSurfaceHelpers.TitledDivider(rightTitle, rightPane.Owns), 0, 2)
            .Cell(leftPane, 1, 0)
            .Cell(StraumrSurfaceHelpers.VerticalDivider(), 1, 1)
            .Cell(rightPane, 1, 2)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
    }

    public static Visual FourPaneSections(
        PaneSplits splits,
        (string Title, Visual Pane) leftTop,
        (string Title, Visual Pane) rightTop,
        (string Title, Visual Pane) leftBottom,
        (string Title, Visual Pane) rightBottom)
    {
        splits.UseStack();

        return PaneColumns(
                splits.Sections,
                new RowDefinition { Height = GridLength.Auto },
                splits.Stack.FirstRow(),
                new RowDefinition { Height = GridLength.Auto },
                splits.Stack.SecondRow())
            .Cell(StraumrSurfaceHelpers.TitledDivider(leftTop.Title, leftTop.Pane.Owns), 0, 0)
            .Cell(StraumrSurfaceHelpers.VerticalDivider((0, new Rune('┬'))), 0, 1)
            .Cell(StraumrSurfaceHelpers.TitledDivider(rightTop.Title, rightTop.Pane.Owns), 0, 2)
            .Cell(leftTop.Pane, 1, 0)
            .Cell(StraumrSurfaceHelpers.VerticalDivider(), 1, 1)
            .Cell(rightTop.Pane, 1, 2)
            .Cell(StraumrSurfaceHelpers.TitledDivider(leftBottom.Title, leftBottom.Pane.Owns), 2, 0)
            .Cell(StraumrSurfaceHelpers.VerticalDivider((0, new Rune('┼'))), 2, 1)
            .Cell(StraumrSurfaceHelpers.TitledDivider(rightBottom.Title, rightBottom.Pane.Owns), 2, 2)
            .Cell(leftBottom.Pane, 3, 0)
            .Cell(StraumrSurfaceHelpers.VerticalDivider(), 3, 1)
            .Cell(rightBottom.Pane, 3, 2)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
    }

    public static Visual EmptySections() => StraumrSurfaceHelpers.HorizontalDivider();

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

    private static Visual BuildListPanel(
        string listTitle,
        Func<string> listCount,
        ResourceFilter filter,
        Func<Visual> listContent)
    {
        Grid panel = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() });

        Visual bar = StraumrSurfaceHelpers.Bar(
            filter.Root,
            new TextBlock(() => $" {listCount()} ").Style(StraumrStyleService.TokenChip));

        return panel
            .Cell(StraumrSurfaceHelpers.Inset(bar, PaneInset), 0, 0)
            .Cell(StraumrSurfaceHelpers.TitledDivider(listTitle, panel.Owns), 1, 0)
            .Cell(
                StraumrSurfaceHelpers.Inset(
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
            .Cell(StraumrSurfaceHelpers.Inset(
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
