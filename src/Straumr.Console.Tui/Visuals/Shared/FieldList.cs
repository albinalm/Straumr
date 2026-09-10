using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// The label and value grid used by every detail pane. Labels size to the widest of them so values
/// line up in a single column.
/// </summary>
internal static class FieldList
{
    public static Visual Create(params (string Label, Visual Value)[] fields)
    {
        var grid = new Grid()
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
            grid.Cell(new TextBlock(fields[row].Label).Style(StraumrStyles.MutedText), row, 0);
            grid.Cell(fields[row].Value, row, 1);
        }

        return grid;
    }

    public static Visual Text(string value) =>
        new TextBlock(value)
            .Style(StraumrStyles.PrimaryText)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    /// <summary>A value long enough to wrap, ellipsized when the region is too short to wrap into.</summary>
    public static Visual Wrapped(string value) =>
        new TextBlock(value)
            .Style(StraumrStyles.PrimaryText)
            .Wrap(true)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    /// <summary>Shares the resource list's semantics: a populated count is amber, an empty one inert.</summary>
    public static Visual Count(int count) =>
        new TextBlock(count.ToString())
            .Style(count > 0 ? StraumrStyles.AmberText : StraumrStyles.MutedText)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);
}
