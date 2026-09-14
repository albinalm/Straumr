using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// A pane whose width is the grid's decision rather than its content's.
/// </summary>
/// <remarks>
/// A <see cref="GridLength.Star"/> weight is only a weight: a child that reports a large minimum
/// width still takes what it asks for and its sibling shrinks to whatever it will accept. The
/// framework's <c>TabControl</c> asks for the whole row, which is why the 48/52 authentication and
/// request split rendered as roughly 17/83 and the authentication values wrapped four characters
/// wide. Reporting a minimum of one cell and letting the content measure at the width it is given
/// puts the declared weights back in charge.
/// </remarks>
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
            growX: 1,
            growY: 1,
            shrinkX: 1,
            shrinkY: 1);
    }

    protected override void ArrangeCore(in Rectangle finalRect) => _content.Arrange(finalRect);
}
