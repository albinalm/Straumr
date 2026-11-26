using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Helpers;

internal static class StraumrSurfaceHelpers
{

    public static readonly Thickness RowInset = new(1, 0, 1, 0);

    public static Visual Surface(Visual content) =>
        StraumrStyleService.Background.Kind == ColorKind.Default ? new TerminalSurface(content) : content;

    public static Visual VerticalDivider() => new JunctionDivider();

    public static JunctionRule HorizontalDivider()
    {
        var rule = new JunctionRule();
        rule.SetStyle(StraumrStyleService.Divider);
        return rule;
    }

    public static JunctionRule TitledDivider(string title, Func<bool>? isFocused = null)
    {
        JunctionRule rule = HorizontalDivider();
        if (!StraumrStyleService.IsModern)
        {
            rule.StartLabel = FocusTitle(title, isFocused);
            return rule;
        }

        bool Focused() => isFocused?.Invoke() is true;
        rule.StartLabel = new TextBlock(title.ToUpperInvariant())
            .Style(() => Focused() ? StraumrStyleService.FocusedSectionText : StraumrStyleService.IdleSectionText);
        rule.SetStyle(() => Focused() ? StraumrStyleService.AccentDivider : StraumrStyleService.Divider);
        return rule;
    }

    public static Visual FooterDivider() =>
        StraumrStyleService.IsModern ? new Padder().Padding(new Thickness(0)) : HorizontalDivider();

    public static JunctionRule TitledDivider(string title, Visual pane, Func<bool>? isFocused = null) =>
        TitledDivider(title, isFocused ?? pane.Owns).PointerPressed((_, e) =>
        {
            if (e.Button == TerminalMouseButton.Left && pane.FocusInto())
            {
                e.Handled = true;
            }
        });

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
