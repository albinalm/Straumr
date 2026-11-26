using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal sealed class JunctionDivider : Visual
{
    public JunctionDivider()
    {
        HorizontalAlignment = Align.Stretch;
        VerticalAlignment = Align.Stretch;
        ChromeJunctions.Add(this);
    }

    private static Style JoinStyle(JunctionRule? left, JunctionRule? right)
    {
        Style idle = StraumrStyleService.DividerCell;
        if (left?.LineStyle is { } leftStyle && leftStyle != idle)
        {
            return leftStyle;
        }

        return right?.LineStyle is { } rightStyle && rightStyle != idle ? rightStyle : idle;
    }

    protected override SizeHints MeasureCore(in LayoutConstraints constraints) =>
        SizeHints.Flex(new Size(1, 0), new Size(1, 1), new Size(1, LayoutConstraints.Unbounded.MaxHeight), 0, 1, 0, 1);

    protected override void RenderOverride(CellBuffer buffer)
    {
        Rectangle bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        List<JunctionRule> rules = ChromeJunctions.RulesNear(this);
        List<JunctionDivider> dividers = ChromeJunctions.DividersNear(this);
        int x = bounds.X;
        bool above = dividers.Any(divider => ChromeJunctions.Contains(divider, x, bounds.Y - 1));
        bool below = dividers.Any(divider => ChromeJunctions.Contains(divider, x, bounds.Bottom));
        for (int y = bounds.Y; y < bounds.Bottom; y++)
        {
            int row = y;
            bool up = y > bounds.Y || above;
            bool down = y < bounds.Bottom - 1 || below;
            JunctionRule? left = rules.FirstOrDefault(rule => rule.IsLine(x - 1, row));
            JunctionRule? right = rules.FirstOrDefault(rule => rule.IsLine(x + 1, row));
            buffer.SetCell(x, y, ChromeJunctions.Glyph(up, down, left is not null, right is not null), JoinStyle(left, right));
        }
    }
}
