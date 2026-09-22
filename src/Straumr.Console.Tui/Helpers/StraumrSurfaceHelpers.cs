using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Helpers;

internal static class StraumrSurfaceHelpers
{
    private static readonly Rune VerticalLine = new('│');

    public static readonly Thickness RowInset = new(1, 0, 1, 0);

    public static Visual VerticalDivider(params (int Row, Rune Glyph)[] junctions)
    {
        var canvas = new Canvas(context =>
        {
            int height = context.Size.Height;
            context.DrawVLine(0, 0, height, VerticalLine, StraumrStyleService.DividerCell);

            foreach ((int row, Rune glyph) in junctions)
            {
                if (row >= 0 && row < height)
                {
                    context.DrawVLine(0, row, 1, glyph, StraumrStyleService.DividerCell);
                }
            }
        });
        canvas.HorizontalAlignment = Align.Stretch;
        canvas.VerticalAlignment = Align.Stretch;
        return canvas;
    }

    public static Rule HorizontalDivider()
    {
        var rule = new Rule();
        rule.SetStyle(StraumrStyleService.Divider);
        return rule;
    }

    public static Rule TitledDivider(string title, Func<bool>? isFocused = null)
    {
        Rule rule = HorizontalDivider();
        rule.StartLabel = FocusTitle(title, isFocused);
        return rule;
    }

    public static TextBlock FocusTitle(string title, Func<bool>? isFocused) =>
        new TextBlock(() => FocusLabel(title, isFocused))
            .Style(() => isFocused?.Invoke() is true
                ? StraumrStyleService.FocusChip
                : StraumrStyleService.MutedText);

    private static string FocusLabel(string title, Func<bool>? isFocused) =>
        isFocused?.Invoke() is true ? $" {title} " : title;

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
