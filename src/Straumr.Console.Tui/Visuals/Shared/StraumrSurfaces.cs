using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// Background washes and dividers used to build the rule-separated shell. The framework has no
/// vertical rule control and no per-visual background, so both are painted through <see cref="Canvas"/>.
/// </summary>
internal static class StraumrSurfaces
{
    private static readonly Rune Space = new(' ');
    private static readonly Rune VerticalLine = new('│');

    public static Visual Panel(Visual content) => Wash(StraumrStyles.Panel, content);

    public static Visual PanelAlt(Visual content) => Wash(StraumrStyles.PanelAlt, content);

    /// <summary>
    /// A full-height column divider. <paramref name="junctions"/> replaces the line glyph on the rows
    /// where a <see cref="HorizontalDivider"/> meets the column, keyed by row offset within the divider.
    /// </summary>
    public static Visual VerticalDivider(params (int Row, Rune Glyph)[] junctions)
    {
        var canvas = new Canvas(context =>
        {
            int height = context.Size.Height;
            context.DrawVLine(0, 0, height, VerticalLine, StraumrStyles.DividerCell);

            foreach ((int row, Rune glyph) in junctions)
            {
                if (row >= 0 && row < height)
                    context.DrawVLine(0, row, 1, glyph, StraumrStyles.DividerCell);
            }
        });
        canvas.HorizontalAlignment = Align.Stretch;
        canvas.VerticalAlignment = Align.Stretch;
        return canvas;
    }

    public static Rule HorizontalDivider()
    {
        var rule = new Rule();
        rule.SetStyle(StraumrStyles.Divider);
        return rule;
    }

    /// <summary>
    /// A single-row bar with left- and right-aligned content. <see cref="Header"/> is not used because
    /// it forces bold slots and its own surface color.
    /// </summary>
    public static Visual Bar(Visual left, Visual right) =>
        new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star() },
                new ColumnDefinition { Width = GridLength.Auto })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(2)
            .Cell(left, 0, 0)
            .Cell(right, 0, 1)
            .HorizontalAlignment(Align.Stretch);

    public static Visual Inset(Visual content, Thickness padding) =>
        new Padder(content)
            .Padding(padding)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

    private static Visual Wash(Color background, Visual content)
    {
        var fill = new Canvas(context => context.FillRect(
            0,
            0,
            context.Size.Width,
            context.Size.Height,
            Space,
            Style.None.WithBackground(background)));
        fill.HorizontalAlignment = Align.Stretch;
        fill.VerticalAlignment = Align.Stretch;

        return new ZStack(fill, content)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
    }
}
