using System.Text;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Scrolling;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// The multiline resource list shared by every screen. The pinned <c>ListBox&lt;T&gt;</c> fixes every
/// item to one terminal row, so this owns its own layout, scrolling, selection and hover instead.
/// </summary>
public sealed partial class ResourceList : Visual, IScrollable
{
    private const int ItemSpacing = 1;

    /// <summary>Blank cells kept between the panel edges and the selection band.</summary>
    private const int PanelInset = 1;

    /// <summary>The selection bar plus the gap separating it from the item text.</summary>
    private const int MarkerWidth = 2;

    private const int TextInset = PanelInset + MarkerWidth;
    private const int MinimumTextWidth = 8;

    private static readonly Rune SelectionBar = new('▌');
    private static readonly Rune Blank = new(' ');

    private readonly IReadOnlyList<Visual> _items;
    private readonly ScrollModel _scroll;
    private readonly int _itemHeight;
    private readonly int _itemStride;

    public ResourceList(IEnumerable<ResourceRow> rows)
    {
        ResourceRow[] source = rows.ToArray();
        Count = source.Length;
        _itemHeight = source.Any(row => row.Detail is not null) ? 3 : 2;
        _itemStride = _itemHeight + ItemSpacing;
        _items = source.Select(BuildItem).ToArray();
        _scroll = new ScrollModel(this);
        SelectedIndex = Count == 0 ? -1 : 0;
        HoveredIndex = -1;

        foreach (Visual item in _items)
            AttachChild(item);

        Focusable = true;
        HorizontalAlignment = Align.Stretch;
        VerticalAlignment = Align.Stretch;
    }

    public ScrollModel Scroll => _scroll;

    public int Count { get; }

    [Bindable]
    public partial int SelectedIndex { get; set; }

    /// <summary>Index of the row under the pointer, or -1 when the pointer is elsewhere.</summary>
    [Bindable]
    public partial int HoveredIndex { get; set; }

    protected override int ChildrenCount => _items.Count;

    protected override Visual GetChild(int index) => _items[index];

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        int textWidth = Math.Max(MinimumTextWidth, constraints.MaxWidth - TextInset - PanelInset);
        var itemConstraints = new LayoutConstraints(textWidth, textWidth, _itemHeight, _itemHeight);

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
                finalRect.Y + index * _itemStride - _scroll.OffsetY,
                textWidth,
                _itemHeight));
        }
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        if (HoveredIndex >= 0 && HoveredIndex != SelectedIndex)
            PaintBand(buffer, HoveredIndex, StraumrStyles.HoveredItem, null);

        if (SelectedIndex < 0)
            return;

        PaintBand(
            buffer,
            SelectedIndex,
            HasFocus ? StraumrStyles.SelectedItem : StraumrStyles.SelectedItemInactive,
            HasFocus ? StraumrStyles.SelectionMarker : StraumrStyles.SelectionMarkerInactive);
    }

    private void PaintBand(CellBuffer buffer, int index, Style band, Style? marker)
    {
        Rectangle bounds = Bounds;
        int bandLeft = bounds.X + PanelInset;
        int bandRight = Math.Max(bandLeft, bounds.Right - PanelInset);
        int top = bounds.Y + index * _itemStride - _scroll.OffsetY;
        int bottom = top + _itemHeight;

        for (int y = Math.Max(top, bounds.Y); y < Math.Min(bottom, bounds.Bottom); y++)
        {
            for (int x = bandLeft; x < bandRight; x++)
                buffer.SetCell(x, y, Blank, band);

            if (marker is not null)
                buffer.SetCell(bandLeft, y, SelectionBar, marker.Value);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Count == 0)
            return;

        int pageSize = Math.Max(1, Bounds.Height / _itemStride);
        int target = e.Key switch
        {
            TerminalKey.Up => SelectedIndex - 1,
            TerminalKey.Down => SelectedIndex + 1,
            TerminalKey.Home => 0,
            TerminalKey.End => Count - 1,
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

        int index = IndexAt(e.UiY);
        if (index < 0)
            return;

        SelectedIndex = index;
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e) => HoveredIndex = IndexAt(e.UiY);

    protected override void OnHoveredChanged(bool isHovered)
    {
        if (!isHovered)
            HoveredIndex = -1;
    }

    /// <summary>Returns the item index under a UI row, or -1 for the gap between items.</summary>
    private int IndexAt(int uiY)
    {
        int row = uiY - Bounds.Y + _scroll.OffsetY;
        if (row < 0)
            return -1;

        int index = row / _itemStride;
        return (uint)index < (uint)Count && row % _itemStride < _itemHeight
            ? index
            : -1;
    }

    protected override void OnPointerWheel(PointerEventArgs e)
    {
        if (Count == 0 || e.WheelDelta == 0)
            return;

        SelectedIndex += e.WheelDelta > 0 ? -1 : 1;
        e.Handled = true;
    }

    private void EnsureSelectedVisible()
    {
        if (SelectedIndex < 0 || _scroll.ViewportHeight <= 0)
            return;

        int itemTop = SelectedIndex * _itemStride;
        int itemBottom = itemTop + _itemHeight;
        int viewportBottom = _scroll.OffsetY + _scroll.ViewportHeight;

        if (itemTop < _scroll.OffsetY)
            _scroll.SetOffset(_scroll.OffsetX, itemTop);
        else if (itemBottom > viewportBottom)
            _scroll.SetOffset(_scroll.OffsetX, itemBottom - _scroll.ViewportHeight);
    }

    partial void OnSelectedIndexChanging(ref int value)
    {
        value = Count == 0
            ? -1
            : Math.Clamp(value, 0, Count - 1);
    }

    partial void OnSelectedIndexChanged(int value) => EnsureSelectedVisible();

    partial void OnHoveredIndexChanging(ref int value)
    {
        if (value < 0 || value >= Count)
            value = -1;
    }

    private int ContentHeight =>
        _items.Count == 0
            ? 0
            : _items.Count * _itemStride - ItemSpacing;

    /// <summary>
    /// Row styles are resolved per frame from <see cref="SelectedIndex"/> so the selected row reads
    /// brighter against the selection band, and so a resource holding nothing reads as inert.
    /// </summary>
    private Visual BuildItem(ResourceRow row, int index)
    {
        var stack = new VStack(
                Line(
                    row.Name,
                    () => index == SelectedIndex
                        ? StraumrStyles.BrightText
                        : StraumrStyles.PrimaryText,
                    TextTrimming.EndEllipsis),
                Line(
                    row.Meta,
                    () => row.HasContent
                        ? StraumrStyles.AmberText
                        : index == SelectedIndex
                            ? StraumrStyles.MutedBrightText
                            : StraumrStyles.MutedText,
                    TextTrimming.EndEllipsis))
            .HorizontalAlignment(Align.Stretch);

        if (row.Detail is not null)
        {
            stack.Add(Line(
                row.Detail,
                () => index == SelectedIndex
                    ? StraumrStyles.MutedBrightText
                    : StraumrStyles.MutedText,
                TextTrimming.StartEllipsis));
        }

        return stack;
    }

    private static TextBlock Line(
        string text,
        Func<TextBlockStyle> style,
        TextTrimming trimming) =>
        new TextBlock(text)
            .Style(style)
            .Trimming(trimming)
            .HorizontalAlignment(Align.Stretch);
}
