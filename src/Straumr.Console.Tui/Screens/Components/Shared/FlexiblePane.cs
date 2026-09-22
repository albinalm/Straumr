using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class FlexiblePane : Visual
{
    private readonly Visual _content;

    public FlexiblePane(Visual content)
    {
        _content = content;
        AttachChild(content);
        HorizontalAlignment = Align.Stretch;
        VerticalAlignment = Align.Stretch;
    }

    protected override int ChildrenCount => 1;

    protected override Visual GetChild(int index) =>
        index == 0 ? _content : throw new ArgumentOutOfRangeException(nameof(index));

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        int width = constraints.MaxWidth == LayoutConstraints.Unbounded.MaxWidth
            ? Math.Max(1, constraints.MinWidth)
            : Math.Max(1, constraints.MaxWidth);

        _content.Measure(new LayoutConstraints(
            width,
            width,
            constraints.MinHeight,
            constraints.MaxHeight));

        return SizeHints.Flex(
            new Size(1, 1),
            new Size(width, Math.Max(1, _content.DesiredSize.Height)),
            new Size(
                LayoutConstraints.Unbounded.MaxWidth,
                LayoutConstraints.Unbounded.MaxHeight),
            1,
            1,
            1,
            1);
    }

    protected override void ArrangeCore(in Rectangle finalRect) => _content.Arrange(finalRect);
}
