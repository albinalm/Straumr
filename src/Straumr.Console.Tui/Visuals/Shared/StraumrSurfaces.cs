using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// Dividers and insets used to build the rule-separated shell. The framework has no vertical rule
/// control, so the column divider is painted through a <see cref="Canvas"/> painter.
/// </summary>
internal static class StraumrSurfaces
{
    private static readonly Rune VerticalLine = new('│');

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
    /// A divider carrying a section title on the line itself. The title fills with the focus chip
    /// while its section owns focus and is otherwise inert.
    /// </summary>
    /// <param name="isFocused">
    /// Whether the titled section owns focus. Omitted for a section that cannot take focus, which
    /// then never renders as focused; the previous default did the opposite and made a permanently
    /// bright title compete with the section actually holding focus.
    /// </param>
    public static Rule TitledDivider(string title, Func<bool>? isFocused = null)
    {
        Rule rule = HorizontalDivider();
        rule.StartLabel = new TextBlock(() => FocusLabel(title, isFocused))
            .Style(() => isFocused?.Invoke() is true
                ? StraumrStyles.FocusChip
                : StraumrStyles.MutedText);
        return rule;
    }

    /// <summary>
    /// Pads a title so the focus chip has a cell of fill on each side of the text. The unfocused
    /// title stays unpadded because it paints no background to breathe inside.
    /// </summary>
    public static string FocusLabel(string title, Func<bool>? isFocused) =>
        isFocused?.Invoke() is true ? $" {title} " : title;

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
}
