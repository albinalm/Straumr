using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Scrolling;

namespace Straumr.Console.Tui.Screens.Components.Shared;

public sealed partial class ScrollableContent : Visual, IScrollable
{
    private static readonly Rune ScrollTrack = new('░');
    private static readonly Rune ScrollThumb = new('█');

    private readonly Visual _content;

    public ScrollableContent(Visual content, bool hints = true)
    {
        _content = content;
        Scroll = new ScrollModel(this);
        AttachChild(content);
        Focusable = true;
        this.IsTabStop(this.IsReachable);
        HorizontalAlignment = Align.Stretch;
        VerticalAlignment = Align.Stretch;

        if (!hints)
        {
            return;
        }

        AddCommand(Hint("ScrollableContent.Next", "Scroll down",
            () => ScrollBy(1)));
        AddCommand(Hint("ScrollableContent.Previous", "Scroll up",
            () => ScrollBy(-1)));
        AddCommand(Hint("ScrollableContent.Top", "Top",
            () => ScrollTo(0), CommandImportance.Secondary));
        AddCommand(Hint("ScrollableContent.Bottom", "Bottom",
            () => ScrollTo(Scroll.ExtentHeight), CommandImportance.Secondary));
    }

    [Bindable]
    public partial int ScrollOffset { get; set; }

    protected override int ChildrenCount => 1;

    public ScrollModel Scroll { get; }

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
            1,
            1,
            1,
            1);
    }

    protected override void ArrangeCore(in Rectangle finalRect)
    {
        int contentHeight = Math.Max(finalRect.Height, _content.DesiredSize.Height);
        Scroll.SetViewport(finalRect.Width, finalRect.Height);
        Scroll.SetExtent(finalRect.Width, contentHeight);
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
        if (Scroll.ExtentHeight <= Scroll.ViewportHeight || bounds.Height <= 0)
        {
            return;
        }

        int x = bounds.Right - 1;
        int thumbHeight = Math.Max(
            1,
            Scroll.ViewportHeight * Scroll.ViewportHeight / Scroll.ExtentHeight);
        int travel = Math.Max(0, Scroll.ViewportHeight - thumbHeight);
        int scrollRange = Math.Max(1, Scroll.ExtentHeight - Scroll.ViewportHeight);
        int thumbTop = bounds.Y + ScrollOffset * travel / scrollRange;

        for (int y = bounds.Y; y < bounds.Bottom; y++)
        {
            bool isThumb = y >= thumbTop && y < thumbTop + thumbHeight;
            buffer.SetCell(
                x,
                y,
                isThumb ? ScrollThumb : ScrollTrack,
                isThumb ? StraumrStyleService.ScrollThumbCell : StraumrStyleService.ScrollTrackCell);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int pageSize = Math.Max(1, Scroll.ViewportHeight);
        int? target =
            TuiKeybindHelpers.Matches("ScrollableContent.Next", e) ? ScrollOffset + 1 :
            TuiKeybindHelpers.Matches("ScrollableContent.Previous", e) ? ScrollOffset - 1 :
            TuiKeybindHelpers.Matches("ScrollableContent.Top", e) ? 0 :
            TuiKeybindHelpers.Matches("ScrollableContent.Bottom", e) ? Scroll.ExtentHeight :
            TuiKeybindHelpers.Matches("ScrollableContent.Up", e) ? ScrollOffset - 1 :
            TuiKeybindHelpers.Matches("ScrollableContent.Down", e) ? ScrollOffset + 1 :
            TuiKeybindHelpers.Matches("ScrollableContent.Home", e) ? 0 :
            TuiKeybindHelpers.Matches("ScrollableContent.End", e) ? Scroll.ExtentHeight :
            TuiKeybindHelpers.Matches("ScrollableContent.PageUp", e) ? ScrollOffset - pageSize :
            TuiKeybindHelpers.Matches("ScrollableContent.PageDown", e) ? ScrollOffset + pageSize :
            null;

        if (target is null)
        {
            return;
        }

        ScrollTo(target.Value);
        e.Handled = true;
    }

    protected override void OnPointerWheel(PointerEventArgs e)
    {
        if (e.WheelDelta == 0)
        {
            return;
        }

        ScrollBy(e.WheelDelta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void ScrollBy(int delta) => ScrollTo(ScrollOffset + delta);

    private void ScrollTo(int offset)
    {
        int maximum = Math.Max(0, _content.DesiredSize.Height - Bounds.Height);
        ScrollOffset = Math.Clamp(offset, 0, maximum);
        Scroll.SetOffset(0, ScrollOffset);
    }

    private static Command Hint(
        string id,
        string label,
        Action execute,
        CommandImportance importance = CommandImportance.Primary) =>
        new()
        {
            Id = id,
            LabelMarkup = label,
            Gesture = TuiKeybindHelpers.Get(id),
            Importance = importance,
            Presentation = CommandPresentation.CommandBar,
            RouteGesture = false,
            Execute = _ => execute()
        };
}
