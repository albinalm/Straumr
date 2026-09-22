using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Helpers;

internal static class FieldListHelpers
{
    public static Visual Create(params (string Label, Visual Value)[] fields)
    {
        Grid grid = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(fields
                .Select(_ => new RowDefinition { Height = GridLength.Auto })
                .ToArray())
            .ColumnGap(2)
            .HorizontalAlignment(Align.Stretch);

        for (int row = 0; row < fields.Length; row++)
        {
            grid.Cell(new TextBlock(fields[row].Label).Style(StraumrStyleService.MutedText), row, 0);
            grid.Cell(fields[row].Value, row, 1);
        }

        return grid;
    }

    public static Visual Text(string value) =>
        new TextBlock(value)
            .Style(StraumrStyleService.PrimaryText)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    public static Visual Wrapped(string value) =>
        new TextBlock(value)
            .Style(StraumrStyleService.PrimaryText)
            .Wrap(true)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    public static Visual Problem(string value) =>
        new TextBlock(value)
            .Style(StraumrStyleService.RedText)
            .Wrap(true)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    public static Visual Styled(string value, TextBlockStyle style) =>
        new TextBlock(value)
            .Style(style)
            .Wrap(true)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    public static Visual Count(int count) =>
        new TextBlock(count.ToString())
            .Style(count > 0 ? StraumrStyleService.AmberText : StraumrStyleService.MutedText)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);
}
