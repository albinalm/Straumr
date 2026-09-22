using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Layout;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class CommandPromptRow : Visual, IModalVisual
{
    private readonly Visual _close;
    private readonly Visual _editor;

    public CommandPromptRow(Visual editor, Visual close)
    {
        _editor = editor;
        _close = close;
        AttachChild(editor);
        AttachChild(close);
        HorizontalAlignment = Align.Stretch;
    }

    protected override int ChildrenCount => 2;

    public bool IsModal => IsVisible;

    protected override Visual GetChild(int index) => index == 0 ? _editor : _close;

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        _close.Measure(constraints);
        int closeWidth = _close.DesiredSize.Width;

        int available = constraints.IsWidthBounded
            ? Math.Max(0, constraints.MaxWidth - closeWidth)
            : LayoutConstraints.Unbounded.MaxWidth;
        _editor.Measure(new LayoutConstraints(0, available, 1, 1));

        Size size = constraints.Clamp(
            new Size(_editor.DesiredSize.Width + closeWidth, 1));

        return SizeHints.Flex(new Size(closeWidth, 1), size, size, 1, 0, 1, 0);
    }

    protected override void ArrangeCore(in Rectangle finalRect)
    {
        int closeWidth = Math.Min(finalRect.Width, _close.DesiredSize.Width);

        _close.Arrange(new Rectangle(
            finalRect.Right - closeWidth, finalRect.Y, closeWidth, finalRect.Height));
        _editor.Arrange(new Rectangle(
            finalRect.X, finalRect.Y,
            Math.Max(0, finalRect.Width - closeWidth), finalRect.Height));
    }
}
