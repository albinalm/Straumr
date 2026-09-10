using System.Text;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Scrolling;

namespace Straumr.Console.Tui.Visuals.Shared;

public sealed partial class ScrollableContent : Visual, IScrollable
{
    private static readonly Rune ScrollTrack = new('░');
    private static readonly Rune ScrollThumb = new('█');

    private readonly Visual _content;
    private readonly ScrollModel _scroll;

    public ScrollableContent(Visual content)
    {
        _content = content;
        _scroll = new ScrollModel(this);
        AttachChild(content);
        Focusable = true;
        HorizontalAlignment = Align.Stretch;
        VerticalAlignment = Align.Stretch;

        AddCommand(Hint("ScrollableContent.Next", "Scroll down", 'j',
            () => ScrollBy(1)));
        AddCommand(Hint("ScrollableContent.Previous", "Scroll up", 'k',
            () => ScrollBy(-1)));
        AddCommand(Hint("ScrollableContent.Top", "Top", 'g',
            () => ScrollTo(0), CommandImportance.Secondary));
        AddCommand(Hint("ScrollableContent.Bottom", "Bottom", 'G',
            () => ScrollTo(_scroll.ExtentHeight), CommandImportance.Secondary));
    }

    public ScrollModel Scroll => _scroll;

    [Bindable]
    public partial int ScrollOffset { get; set; }

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
            0,
            LayoutConstraints.Unbounded.MaxHeight));

        return SizeHints.Flex(
            new Size(1, 1),
            new Size(width, 1),
            new Size(
                LayoutConstraints.Unbounded.MaxWidth,
                LayoutConstraints.Unbounded.MaxHeight),
            growX: 1,
            growY: 1,
            shrinkX: 1,
            shrinkY: 1);
    }

    protected override void ArrangeCore(in Rectangle finalRect)
    {
        int contentHeight = Math.Max(finalRect.Height, _content.DesiredSize.Height);
        _scroll.SetViewport(finalRect.Width, finalRect.Height);
        _scroll.SetExtent(finalRect.Width, contentHeight);
        int offset = Math.Clamp(ScrollOffset, 0, contentHeight - finalRect.Height);
        bool showScrollBar = contentHeight > finalRect.Height;
        _content.Arrange(new Rectangle(
            finalRect.X,
            finalRect.Y - offset,
            Math.Max(1, finalRect.Width - (showScrollBar ? 1 : 0)),
            contentHeight));
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        Rectangle bounds = Bounds;
        if (_scroll.ExtentHeight <= _scroll.ViewportHeight || bounds.Height <= 0)
            return;

        int x = bounds.Right - 1;
        int thumbHeight = Math.Max(
            1,
            _scroll.ViewportHeight * _scroll.ViewportHeight / _scroll.ExtentHeight);
        int travel = Math.Max(0, _scroll.ViewportHeight - thumbHeight);
        int scrollRange = Math.Max(1, _scroll.ExtentHeight - _scroll.ViewportHeight);
        int thumbTop = bounds.Y + ScrollOffset * travel / scrollRange;

        for (int y = bounds.Y; y < bounds.Bottom; y++)
        {
            bool isThumb = y >= thumbTop && y < thumbTop + thumbHeight;
            buffer.SetCell(
                x,
                y,
                isThumb ? ScrollThumb : ScrollTrack,
                isThumb ? StraumrStyles.ScrollThumbCell : StraumrStyles.ScrollTrackCell);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int pageSize = Math.Max(1, _scroll.ViewportHeight);
        int? target = e.Char switch
        {
            'j' => ScrollOffset + 1,
            'k' => ScrollOffset - 1,
            'g' => 0,
            'G' => _scroll.ExtentHeight,
            _ => e.Key switch
            {
                TerminalKey.Up => ScrollOffset - 1,
                TerminalKey.Down => ScrollOffset + 1,
                TerminalKey.Home => 0,
                TerminalKey.End => _scroll.ExtentHeight,
                TerminalKey.PageUp => ScrollOffset - pageSize,
                TerminalKey.PageDown => ScrollOffset + pageSize,
                _ => null
            }
        };

        if (target is null)
            return;

        ScrollTo(target.Value);
        e.Handled = true;
    }

    protected override void OnPointerWheel(PointerEventArgs e)
    {
        if (e.WheelDelta == 0)
            return;

        ScrollBy(e.WheelDelta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void ScrollBy(int delta) => ScrollTo(ScrollOffset + delta);

    private void ScrollTo(int offset)
    {
        int maximum = Math.Max(0, _content.DesiredSize.Height - Bounds.Height);
        ScrollOffset = Math.Clamp(offset, 0, maximum);
        _scroll.SetOffset(0, ScrollOffset);
    }

    private static Command Hint(
        string id,
        string label,
        char gesture,
        Action execute,
        CommandImportance importance = CommandImportance.Primary) =>
        new()
        {
            Id = id,
            LabelMarkup = label,
            Gesture = new KeyGesture(gesture),
            Importance = importance,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => execute()
        };
}
