using System.Text;
using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Scrolling;

namespace Straumr.Console.Tui.Screens.Workspace;

public sealed partial class WorkspaceList : Visual, IScrollable
{
    private const int ItemHeight = 3;
    private const int ItemSpacing = 1;
    private const int ItemStride = ItemHeight + ItemSpacing;
    private const int MarkerWidth = 2;

    private readonly IReadOnlyList<WorkspaceScreenItem> _workspaces;
    private readonly IReadOnlyList<Visual> _items;
    private readonly ScrollModel _scroll;

    public WorkspaceList(IEnumerable<WorkspaceScreenItem> workspaces)
    {
        _workspaces = workspaces.ToArray();
        _items = _workspaces.Select(BuildItem).ToArray();
        _scroll = new ScrollModel(this);
        SelectedIndex = _workspaces.Count == 0 ? -1 : 0;

        foreach (Visual item in _items)
            AttachChild(item);

        Focusable = true;
        HorizontalAlignment = Align.Stretch;
        VerticalAlignment = Align.Stretch;
    }

    public ScrollModel Scroll => _scroll;

    [Bindable]
    public partial int SelectedIndex { get; set; }

    protected override int ChildrenCount => _items.Count;

    protected override Visual GetChild(int index) => _items[index];

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        int contentWidth = 0;
        var itemConstraints = new LayoutConstraints(
            0,
            LayoutConstraints.Unbounded.MaxWidth,
            ItemHeight,
            ItemHeight);

        foreach (Visual item in _items)
        {
            item.Measure(itemConstraints);
            contentWidth = Math.Max(contentWidth, item.DesiredSize.Width);
        }

        var natural = new Size(
            Math.Max(MarkerWidth, contentWidth + MarkerWidth),
            Math.Max(1, ContentHeight));

        return SizeHints.Flex(
            new Size(MarkerWidth, 1),
            natural,
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
        if (finalRect.Width <= 0 || finalRect.Height <= 0 || _items.Count == 0)
        {
            _scroll.SetViewport(0, 0);
            _scroll.SetExtent(0, 0);
            return;
        }

        int contentWidth = Math.Max(0, finalRect.Width - MarkerWidth);
        int extentWidth = Math.Max(finalRect.Width, DesiredSize.Width);

        _scroll.SetViewport(finalRect.Width, finalRect.Height);
        _scroll.SetExtent(extentWidth, ContentHeight);
        EnsureSelectedVisible();

        for (int index = 0; index < _items.Count; index++)
        {
            int y = finalRect.Y + index * ItemStride - _scroll.OffsetY;
            _items[index].Arrange(new Rectangle(
                finalRect.X + MarkerWidth - _scroll.OffsetX,
                y,
                contentWidth,
                ItemHeight));
        }
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        Rectangle bounds = Bounds;

        for (int index = 0; index < _items.Count; index++)
        {
            if (index != SelectedIndex)
                continue;

            int top = bounds.Y + index * ItemStride - _scroll.OffsetY;
            int bottom = top + ItemHeight;

            if (bottom <= bounds.Y || top >= bounds.Bottom)
                continue;

            for (int y = Math.Max(top, bounds.Y); y < Math.Min(bottom, bounds.Bottom); y++)
            {
                for (int x = bounds.X; x < bounds.Right; x++)
                    buffer.SetCell(x, y, new Rune(' '), StraumrStyles.SelectedItem);

                buffer.SetCell(bounds.X, y, new Rune('▌'), StraumrStyles.SelectionMarker);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_workspaces.Count == 0)
            return;

        int pageSize = Math.Max(1, Bounds.Height / ItemStride);
        int target = e.Key switch
        {
            TerminalKey.Up => SelectedIndex - 1,
            TerminalKey.Down => SelectedIndex + 1,
            TerminalKey.Home => 0,
            TerminalKey.End => _workspaces.Count - 1,
            TerminalKey.PageUp => SelectedIndex - pageSize,
            TerminalKey.PageDown => SelectedIndex + pageSize,
            _ => SelectedIndex
        };

        if (target == SelectedIndex)
            return;

        SelectedIndex = target;
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerEventArgs e)
    {
        if (e.Button != TerminalMouseButton.Left)
            return;

        int row = e.UiY - Bounds.Y + _scroll.OffsetY;
        int index = row / ItemStride;

        if ((uint)index >= (uint)_workspaces.Count || row % ItemStride >= ItemHeight)
            return;

        SelectedIndex = index;
        e.Handled = true;
    }

    protected override void OnPointerWheel(PointerEventArgs e)
    {
        if (_workspaces.Count == 0 || e.WheelDelta == 0)
            return;

        SelectedIndex += e.WheelDelta > 0 ? -1 : 1;
        e.Handled = true;
    }

    private void EnsureSelectedVisible()
    {
        if (SelectedIndex < 0 || _scroll.ViewportHeight <= 0)
            return;

        int itemTop = SelectedIndex * ItemStride;
        int itemBottom = itemTop + ItemHeight;
        int viewportBottom = _scroll.OffsetY + _scroll.ViewportHeight;

        if (itemTop < _scroll.OffsetY)
            _scroll.SetOffset(_scroll.OffsetX, itemTop);
        else if (itemBottom > viewportBottom)
            _scroll.SetOffset(_scroll.OffsetX, itemBottom - _scroll.ViewportHeight);
    }

    partial void OnSelectedIndexChanging(ref int value)
    {
        value = _workspaces.Count == 0
            ? -1
            : Math.Clamp(value, 0, _workspaces.Count - 1);
    }

    partial void OnSelectedIndexChanged(int value) => EnsureSelectedVisible();

    private int ContentHeight =>
        _items.Count == 0
            ? 0
            : _items.Count * ItemStride - ItemSpacing;

    private static Visual BuildItem(WorkspaceScreenItem item) =>
        new VStack(
                new TextBlock(item.Workspace.Name)
                    .Style(StraumrStyles.PrimaryText),
                new TextBlock(
                        $"{CountLabel(item.Workspace.Requests.Count, "request")} · {CountLabel(item.Workspace.Auths.Count, "auth")}")
                    .Style(StraumrStyles.YellowText),
                new TextBlock(item.DisplayDirectory)
                    .Style(StraumrStyles.MutedText))
            .HorizontalAlignment(Align.Stretch);

    private static string CountLabel(int count, string noun) =>
        count == 0
            ? $"no {noun}s"
            : $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
}
