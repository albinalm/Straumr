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

    /// <summary>Blank cells kept between the panel edges and the selection band.</summary>
    private const int PanelInset = 1;

    /// <summary>The selection bar plus the gap separating it from the item text.</summary>
    private const int MarkerWidth = 2;

    private const int TextInset = PanelInset + MarkerWidth;
    private const int MinimumTextWidth = 8;

    private static readonly Rune SelectionBar = new('▌');
    private static readonly Rune Blank = new(' ');

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
        int textWidth = Math.Max(MinimumTextWidth, constraints.MaxWidth - TextInset - PanelInset);
        var itemConstraints = new LayoutConstraints(textWidth, textWidth, ItemHeight, ItemHeight);

        foreach (Visual item in _items)
            item.Measure(itemConstraints);

        var natural = new Size(
            textWidth + TextInset + PanelInset,
            Math.Max(1, ContentHeight));

        return SizeHints.Flex(
            new Size(TextInset + MinimumTextWidth + PanelInset, 1),
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

        int textWidth = Math.Max(
            MinimumTextWidth,
            finalRect.Width - TextInset - PanelInset);

        _scroll.SetViewport(finalRect.Width, finalRect.Height);
        _scroll.SetExtent(finalRect.Width, ContentHeight);
        EnsureSelectedVisible();

        for (int index = 0; index < _items.Count; index++)
        {
            _items[index].Arrange(new Rectangle(
                finalRect.X + TextInset,
                finalRect.Y + index * ItemStride - _scroll.OffsetY,
                textWidth,
                ItemHeight));
        }
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        if (SelectedIndex < 0)
            return;

        Rectangle bounds = Bounds;
        int bandLeft = bounds.X + PanelInset;
        int bandRight = Math.Max(bandLeft, bounds.Right - PanelInset);
        int top = bounds.Y + SelectedIndex * ItemStride - _scroll.OffsetY;
        int bottom = top + ItemHeight;

        for (int y = Math.Max(top, bounds.Y); y < Math.Min(bottom, bounds.Bottom); y++)
        {
            for (int x = bandLeft; x < bandRight; x++)
                buffer.SetCell(x, y, Blank, StraumrStyles.SelectedItem);

            buffer.SetCell(bandLeft, y, SelectionBar, StraumrStyles.SelectionMarker);
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
                Line(item.Workspace.Name, StraumrStyles.PrimaryText, TextTrimming.EndEllipsis),
                Line(
                    $"{CountLabel(item.Workspace.Requests.Count, "request")} · {CountLabel(item.Workspace.Auths.Count, "auth")}",
                    StraumrStyles.YellowText,
                    TextTrimming.EndEllipsis),
                Line(item.DisplayDirectory, StraumrStyles.MutedText, TextTrimming.StartEllipsis))
            .HorizontalAlignment(Align.Stretch);

    private static TextBlock Line(
        string text,
        XenoAtom.Terminal.UI.Styling.TextBlockStyle style,
        TextTrimming trimming) =>
        new TextBlock(text)
            .Style(style)
            .Trimming(trimming)
            .HorizontalAlignment(Align.Stretch);

    private static string CountLabel(int count, string noun) =>
        count == 0
            ? $"no {noun}s"
            : $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
}
