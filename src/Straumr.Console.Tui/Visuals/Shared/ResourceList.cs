using System.Text;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
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

    /// <summary>
    /// The current-resource dot, which sits between the selection bar and that gap. The column is
    /// reserved only when a row claims it, so a list with no current resource keeps the width for text.
    /// </summary>
    private const int CurrentMarkerWidth = 1;

    private const int MinimumTextWidth = 8;

    private static readonly Rune SelectionBar = new('▌');
    private static readonly Rune CurrentDot = new('●');
    private static readonly Rune Blank = new(' ');

    /// <summary>
    /// Index the previous left click landed on, or -1 when the last click was not on a row of this
    /// list. A double-click has to hit the same row twice, and <c>ClickCount</c> alone cannot say so.
    /// </summary>
    private int _lastClickedIndex = -1;

    private readonly ResourceRow[] _rows;
    private readonly int _textInset;
    private readonly IReadOnlyList<Visual> _items;
    private readonly ScrollModel _scroll;
    private readonly int _itemHeight;
    private readonly int _itemStride;

    public ResourceList(IEnumerable<ResourceRow> rows)
    {
        ResourceRow[] source = rows.ToArray();
        _rows = source;
        Count = source.Length;
        _itemHeight = source.Any(row => row.Detail is not null) ? 3 : 2;
        _textInset = PanelInset + MarkerWidth +
            (source.Any(row => row.IsCurrent) ? CurrentMarkerWidth : 0);
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

        AddCommand(new Command
        {
            Id = "ResourceList.Next",
            LabelMarkup = "Next",
            Gesture = new KeyGesture('j'),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => MoveSelection(1)
        });
        AddCommand(new Command
        {
            Id = "ResourceList.Previous",
            LabelMarkup = "Prev",
            Gesture = new KeyGesture('k'),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => MoveSelection(-1)
        });
        AddCommand(new Command
        {
            Id = "ResourceList.Activate",
            LabelMarkup = "Use",
            Gesture = new KeyGesture(TerminalKey.Enter),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedIndex >= 0,
            Execute = _ => ActivateSelection()
        });
        AddCommand(new Command
        {
            Id = "ResourceList.First",
            LabelMarkup = "First",
            Gesture = new KeyGesture('g'),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => SelectedIndex = 0
        });
        AddCommand(new Command
        {
            Id = "ResourceList.Last",
            LabelMarkup = "Last",
            Gesture = new KeyGesture('G'),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ => SelectedIndex = Count - 1
        });
    }

    public ScrollModel Scroll => _scroll;

    public int Count { get; }

    public event Action<int>? ItemActivated;

    [Bindable]
    public partial int SelectedIndex { get; set; }

    /// <summary>Index of the row under the pointer, or -1 when the pointer is elsewhere.</summary>
    [Bindable]
    public partial int HoveredIndex { get; set; }

    protected override int ChildrenCount => _items.Count;

    protected override Visual GetChild(int index) => _items[index];

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        int textWidth = Math.Max(MinimumTextWidth, constraints.MaxWidth - _textInset - PanelInset);
        var itemConstraints = new LayoutConstraints(textWidth, textWidth, _itemHeight, _itemHeight);

        foreach (Visual item in _items)
            item.Measure(itemConstraints);

        var natural = new Size(
            textWidth + _textInset + PanelInset,
            Math.Max(1, ContentHeight));

        return SizeHints.Flex(
            new Size(_textInset + MinimumTextWidth + PanelInset, 1),
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
            finalRect.Width - _textInset - PanelInset);

        _scroll.SetViewport(finalRect.Width, finalRect.Height);
        _scroll.SetExtent(finalRect.Width, ContentHeight);
        EnsureSelectedVisible();

        for (int index = 0; index < _items.Count; index++)
        {
            _items[index].Arrange(new Rectangle(
                finalRect.X + _textInset,
                finalRect.Y + index * _itemStride - _scroll.OffsetY,
                textWidth,
                _itemHeight));
        }
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        if (HoveredIndex >= 0 && HoveredIndex != SelectedIndex)
            PaintBand(buffer, HoveredIndex, StraumrStyles.HoveredItem, null);

        if (SelectedIndex >= 0)
        {
            PaintBand(
                buffer,
                SelectedIndex,
                HasFocus ? StraumrStyles.SelectedItem : StraumrStyles.SelectedItemInactive,
                HasFocus ? StraumrStyles.SelectionMarker : StraumrStyles.SelectionMarkerInactive);
        }

        PaintCurrentMarkers(buffer);
    }

    /// <summary>
    /// Paints the current-resource dot on each claiming row's middle line, over whatever band the row
    /// already carries, so the marker composes with selection and hover rather than competing.
    /// </summary>
    private void PaintCurrentMarkers(CellBuffer buffer)
    {
        if (_textInset == PanelInset + MarkerWidth)
            return;

        Rectangle bounds = Bounds;
        int x = bounds.X + PanelInset + 1;

        for (int index = 0; index < _rows.Length; index++)
        {
            if (!_rows[index].IsCurrent)
                continue;

            int y = bounds.Y + index * _itemStride + _itemHeight / 2 - _scroll.OffsetY;
            if (y < bounds.Y || y >= bounds.Bottom)
                continue;

            buffer.SetCell(x, y, CurrentDot, StraumrStyles.CurrentMarker(RowBackground(index)));
        }
    }

    private Color RowBackground(int index)
    {
        if (index == SelectedIndex)
            return HasFocus ? StraumrStyles.Selection : StraumrStyles.SelectionInactive;

        return index == HoveredIndex ? StraumrStyles.Hover : StraumrStyles.Background;
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

        if (e.Key == TerminalKey.Enter)
        {
            ActivateSelection();
            e.Handled = true;
            return;
        }

        int pageSize = Math.Max(1, Bounds.Height / _itemStride);
        int target = e.Char switch
        {
            'j' => SelectedIndex + 1,
            'k' => SelectedIndex - 1,
            'g' => 0,
            'G' => Count - 1,
            _ => e.Key switch
            {
                TerminalKey.Up => SelectedIndex - 1,
                TerminalKey.Down => SelectedIndex + 1,
                TerminalKey.Home => 0,
                TerminalKey.End => Count - 1,
                TerminalKey.PageUp => SelectedIndex - pageSize,
                TerminalKey.PageDown => SelectedIndex + pageSize,
                _ => SelectedIndex
            }
        };

        if (target == SelectedIndex)
            return;

        SelectedIndex = target;
        e.Handled = true;
    }

    /// <remarks>
    /// A double-click activates the row, the pointer equivalent of <c>Enter</c>. <c>ClickCount</c>
    /// counts a click sequence by time alone, so a click anywhere followed by a click on a row
    /// arrives here as the second of a pair; requiring both clicks to land on the same row is what
    /// makes the gesture a real double-click. The test is for exactly two clicks so a longer run
    /// activates once rather than once per press past the second.
    /// </remarks>
    protected override void OnPointerPressed(PointerEventArgs e)
    {
        if (e.Button != TerminalMouseButton.Left)
            return;

        int index = IndexAt(e.UiY);
        if (index < 0)
        {
            _lastClickedIndex = -1;
            return;
        }

        SelectedIndex = index;

        bool isSecondClickOnSameRow = e.ClickCount == 2 && _lastClickedIndex == index;
        _lastClickedIndex = index;
        e.Handled = true;

        if (isSecondClickOnSameRow)
            ActivateSelection();
    }

    protected override void OnPointerMoved(PointerEventArgs e) => HoveredIndex = IndexAt(e.UiY);

    protected override void OnHoveredChanged(bool isHovered)
    {
        if (isHovered)
            return;

        HoveredIndex = -1;

        // The pointer left, so whatever it clicks next cannot be the second half of a double-click
        // on a row of this list, however soon it happens.
        _lastClickedIndex = -1;
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

    private void MoveSelection(int delta)
    {
        if (Count > 0)
            SelectedIndex += delta;
    }

    private void ActivateSelection()
    {
        if (SelectedIndex >= 0)
            ItemActivated?.Invoke(SelectedIndex);
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
