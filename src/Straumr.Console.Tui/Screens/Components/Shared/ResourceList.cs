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

namespace Straumr.Console.Tui.Screens.Components.Shared;

public sealed partial class ResourceList : Visual, IScrollable
{
    private const int MultilineItemSpacing = 1;

    private const int PanelInset = 1;

    private const int MarkerWidth = 2;

    private const int CurrentMarkerWidth = 1;

    private const int MinimumTextWidth = 8;

    private static readonly Rune SelectionBar = new('▌');
    private static readonly Rune CurrentDot = new('●');
    private static readonly Rune Blank = new(' ');
    private readonly Visual? _emptyContent;
    private int _itemHeight;
    private int _itemSpacing;
    private int _itemStride;
    private IReadOnlyList<Visual> _items = [];

    private int _lastClickedIndex = -1;

    private int _pressesOnClickedIndex;

    private ResourceRowModel[] _rows = [];
    private int _textInset;

    public ResourceList(
        IEnumerable<ResourceRowModel> rows,
        Visual? emptyContent = null,
        string activateLabel = "Use")
    {
        _emptyContent = emptyContent;
        Scroll = new ScrollModel(this);
        HoveredIndex = -1;

        if (_emptyContent is not null)
        {
            AttachChild(_emptyContent);
        }

        Focusable = true;
        this.IsTabStop(this.IsReachable);
        HorizontalAlignment = Align.Stretch;
        VerticalAlignment = Align.Stretch;

        // Commands advertise keys only; local handling distinguishes lowercase g from uppercase G.
        AddCommand(new Command
        {
            Id = "ResourceList.Next",
            LabelMarkup = "Next",
            Gesture = TuiKeybindHelpers.Get("ResourceList.Next"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            RouteGesture = false,
            Execute = _ => MoveSelection(1)
        });
        AddCommand(new Command
        {
            Id = "ResourceList.Previous",
            LabelMarkup = "Prev",
            Gesture = TuiKeybindHelpers.Get("ResourceList.Previous"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            RouteGesture = false,
            Execute = _ => MoveSelection(-1)
        });
        AddCommand(new Command
        {
            Id = "ResourceList.Activate",
            LabelMarkup = activateLabel,
            Gesture = TuiKeybindHelpers.Get("ResourceList.Activate"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => SelectedIndex >= 0,
            Execute = _ => TuiKeybindHelpers.Run("ResourceList.Activate", ActivateSelection)
        });
        AddCommand(new Command
        {
            Id = "ResourceList.ActivateAlternate",
            LabelMarkup = activateLabel,
            Gesture = TuiKeybindHelpers.Get("ResourceList.ActivateAlternate"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.None,
            CanExecute = _ => SelectedIndex >= 0,
            RouteGesture = false,
            Execute = _ => TuiKeybindHelpers.Run("ResourceList.ActivateAlternate", ActivateSelection)
        });
        AddCommand(new Command
        {
            Id = "ResourceList.First",
            LabelMarkup = "First",
            Gesture = TuiKeybindHelpers.Get("ResourceList.First"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            RouteGesture = false,
            Execute = _ => SelectedIndex = 0
        });
        AddCommand(new Command
        {
            Id = "ResourceList.Last",
            LabelMarkup = "Last",
            Gesture = TuiKeybindHelpers.Get("ResourceList.Last"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            RouteGesture = false,
            Execute = _ => SelectedIndex = Count - 1
        });

        SetRows(rows);
    }

    public int Count => _rows.Length;

    [Bindable]
    public partial int SelectedIndex { get; set; }

    [Bindable]
    public partial int HoveredIndex { get; set; }

    protected override int ChildrenCount => _items.Count + (_emptyContent is null ? 0 : 1);

    private int ContentHeight =>
        _items.Count == 0
            ? 0
            : _items.Count * _itemStride - _itemSpacing;

    public ScrollModel Scroll { get; }

    public event Action<int>? ItemActivated;

    public void SetRows(IEnumerable<ResourceRowModel> rows)
    {
        foreach (Visual item in _items)
        {
            DetachChild(item);
        }

        _rows = rows.ToArray();
        _itemHeight = _rows.Any(row => row.Detail is not null)
            ? 3
            : _rows.Any(row => row.Meta is not null)
                ? 2
                : 1;
        _textInset = PanelInset + MarkerWidth +
                     (_rows.Any(row => row.IsCurrent) ? CurrentMarkerWidth : 0);
        _itemSpacing = _itemHeight == 1 ? 0 : MultilineItemSpacing;
        _itemStride = _itemHeight + _itemSpacing;
        _items = _rows.Select(BuildItem).ToArray();

        foreach (Visual item in _items)
        {
            AttachChild(item);
        }

        if (_emptyContent is not null)
        {
            _emptyContent.IsVisible = Count == 0;
        }

        SelectedIndex = Count == 0
            ? -1
            : Math.Clamp(SelectedIndex, 0, Count - 1);
        HoveredIndex = -1;
        _lastClickedIndex = -1;
        Scroll.SetOffset(0, 0);
    }

    protected override Visual GetChild(int index) =>
        index < _items.Count
            ? _items[index]
            : _emptyContent ?? throw new ArgumentOutOfRangeException(nameof(index));

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        int textWidth = Math.Max(MinimumTextWidth, constraints.MaxWidth - _textInset - PanelInset);
        var itemConstraints = new LayoutConstraints(textWidth, textWidth, _itemHeight, _itemHeight);

        foreach (Visual item in _items)
        {
            item.Measure(itemConstraints);
        }

        if (_emptyContent is not null && Count == 0)
        {
            _emptyContent.Measure(constraints);
        }

        var natural = new Size(
            textWidth + _textInset + PanelInset,
            Math.Max(1, ContentHeight));

        return SizeHints.Flex(
            new Size(_textInset + MinimumTextWidth + PanelInset, 1),
            natural,
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
        if (finalRect.Width <= 0 || finalRect.Height <= 0)
        {
            Scroll.SetViewport(0, 0);
            Scroll.SetExtent(0, 0);
            return;
        }

        if (_items.Count == 0)
        {
            Scroll.SetViewport(finalRect.Width, finalRect.Height);
            Scroll.SetExtent(finalRect.Width, finalRect.Height);
            _emptyContent?.Arrange(finalRect);
            return;
        }

        int textWidth = Math.Max(
            MinimumTextWidth,
            finalRect.Width - _textInset - PanelInset);

        Scroll.SetViewport(finalRect.Width, finalRect.Height);
        Scroll.SetExtent(finalRect.Width, ContentHeight);
        EnsureSelectedVisible();

        for (int index = 0; index < _items.Count; index++)
        {
            _items[index].Arrange(new Rectangle(
                finalRect.X + _textInset,
                finalRect.Y + index * _itemStride - Scroll.OffsetY,
                textWidth,
                _itemHeight));
        }
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        if (HoveredIndex >= 0 && HoveredIndex != SelectedIndex)
        {
            PaintBand(buffer, HoveredIndex, StraumrStyleService.HoveredItem, null);
        }

        if (SelectedIndex >= 0)
        {
            PaintBand(
                buffer,
                SelectedIndex,
                HasFocus ? StraumrStyleService.SelectedItem : StraumrStyleService.SelectedItemInactive,
                HasFocus ? StraumrStyleService.SelectionMarker : StraumrStyleService.SelectionMarkerInactive);
        }

        PaintCurrentMarkers(buffer);
    }

    private void PaintCurrentMarkers(CellBuffer buffer)
    {
        if (_textInset == PanelInset + MarkerWidth)
        {
            return;
        }

        Rectangle bounds = Bounds;
        int x = bounds.X + PanelInset + 1;

        for (int index = 0; index < _rows.Length; index++)
        {
            if (!_rows[index].IsCurrent)
            {
                continue;
            }

            int y = bounds.Y + index * _itemStride + _itemHeight / 2 - Scroll.OffsetY;
            if (y < bounds.Y || y >= bounds.Bottom)
            {
                continue;
            }

            buffer.SetCell(x, y, CurrentDot, StraumrStyleService.CurrentMarker(RowBackground(index)));
        }
    }

    private Color RowBackground(int index)
    {
        if (index == SelectedIndex)
        {
            return HasFocus ? StraumrStyleService.Selection : StraumrStyleService.SelectionInactive;
        }

        return index == HoveredIndex ? StraumrStyleService.Hover : StraumrStyleService.Background;
    }

    private void PaintBand(CellBuffer buffer, int index, Style band, Style? marker)
    {
        Rectangle bounds = Bounds;
        int bandLeft = bounds.X + PanelInset;
        int bandRight = Math.Max(bandLeft, bounds.Right - PanelInset);
        int top = bounds.Y + index * _itemStride - Scroll.OffsetY;
        int bottom = top + _itemHeight;

        for (int y = Math.Max(top, bounds.Y); y < Math.Min(bottom, bounds.Bottom); y++)
        {
            for (int x = bandLeft; x < bandRight; x++)
            {
                buffer.SetCell(x, y, Blank, band);
            }

            if (marker is not null)
            {
                buffer.SetCell(bandLeft, y, SelectionBar, marker.Value);
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Count == 0)
        {
            return;
        }

        if (TuiKeybindHelpers.Matches("ResourceList.Activate", e) ||
            TuiKeybindHelpers.Matches("ResourceList.ActivateAlternate", e))
        {
            TuiKeybindHelpers.Run(TuiKeybindHelpers.Matches("ResourceList.Activate", e)
                ? "ResourceList.Activate" : "ResourceList.ActivateAlternate", ActivateSelection);
            e.Handled = true;
            return;
        }

        int pageSize = Math.Max(1, Bounds.Height / _itemStride);
        int? target =
            TuiKeybindHelpers.Matches("ResourceList.Next", e) ? SelectedIndex + 1 :
            TuiKeybindHelpers.Matches("ResourceList.Previous", e) ? SelectedIndex - 1 :
            TuiKeybindHelpers.Matches("ResourceList.First", e) ? 0 :
            TuiKeybindHelpers.Matches("ResourceList.Last", e) ? Count - 1 :
            TuiKeybindHelpers.Matches("ResourceList.Up", e) ? SelectedIndex - 1 :
            TuiKeybindHelpers.Matches("ResourceList.Down", e) ? SelectedIndex + 1 :
            TuiKeybindHelpers.Matches("ResourceList.Home", e) ? 0 :
            TuiKeybindHelpers.Matches("ResourceList.End", e) ? Count - 1 :
            TuiKeybindHelpers.Matches("ResourceList.PageUp", e) ? SelectedIndex - pageSize :
            TuiKeybindHelpers.Matches("ResourceList.PageDown", e) ? SelectedIndex + pageSize :
            null;

        if (target is null)
        {
            return;
        }

        SelectedIndex = target.Value;
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerEventArgs e)
    {
        if (e.Button != TerminalMouseButton.Left)
        {
            return;
        }

        int index = IndexAt(e.UiY);
        if (index < 0)
        {
            ForgetClickRun();
            return;
        }

        SelectedIndex = index;
        e.Handled = true;

        if (e.ClickCount < 2)
        {
            _pressesOnClickedIndex = index == _lastClickedIndex ? _pressesOnClickedIndex + 1 : 1;
            _lastClickedIndex = index;
            return;
        }

        if (index != _lastClickedIndex || _pressesOnClickedIndex < 2)
        {
            return;
        }

        ForgetClickRun();
        ActivateSelection();
    }

    private void ForgetClickRun()
    {
        _lastClickedIndex = -1;
        _pressesOnClickedIndex = 0;
    }

    protected override void OnPointerMoved(PointerEventArgs e) => HoveredIndex = IndexAt(e.UiY);

    protected override void OnHoveredChanged(bool isHovered)
    {
        if (isHovered)
        {
            return;
        }

        HoveredIndex = -1;

        ForgetClickRun();
    }

    private int IndexAt(int uiY)
    {
        int row = uiY - Bounds.Y + Scroll.OffsetY;
        if (row < 0)
        {
            return -1;
        }

        int index = row / _itemStride;
        return (uint)index < (uint)Count && row % _itemStride < _itemHeight
            ? index
            : -1;
    }

    protected override void OnPointerWheel(PointerEventArgs e)
    {
        if (Count == 0 || e.WheelDelta == 0)
        {
            return;
        }

        SelectedIndex += e.WheelDelta > 0 ? -1 : 1;
        e.Handled = true;
    }

    private void MoveSelection(int delta)
    {
        if (Count > 0)
        {
            SelectedIndex += delta;
        }
    }

    private void ActivateSelection()
    {
        if (SelectedIndex >= 0)
        {
            ItemActivated?.Invoke(SelectedIndex);
        }
    }

    private void EnsureSelectedVisible()
    {
        if (SelectedIndex < 0 || Scroll.ViewportHeight <= 0)
        {
            return;
        }

        int itemTop = SelectedIndex * _itemStride;
        int itemBottom = itemTop + _itemHeight;
        int viewportBottom = Scroll.OffsetY + Scroll.ViewportHeight;

        if (itemTop < Scroll.OffsetY)
        {
            Scroll.SetOffset(Scroll.OffsetX, itemTop);
        }
        else if (itemBottom > viewportBottom)
        {
            Scroll.SetOffset(Scroll.OffsetX, itemBottom - Scroll.ViewportHeight);
        }
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
        {
            value = -1;
        }
    }

    private Visual BuildItem(ResourceRowModel row, int index)
    {
        Visual name = Line(
            row.Name,
            () => row.IsBroken
                ? index == SelectedIndex
                    ? StraumrStyleService.RedBrightText
                    : StraumrStyleService.RedText
                : index == SelectedIndex
                    ? StraumrStyleService.BrightText
                    : StraumrStyleService.PrimaryText,
            TextTrimming.EndEllipsis);

        if (row.LeadingToken is { } token)
        {
            name = new Grid()
                .Columns(
                    new ColumnDefinition { Width = GridLength.Fixed(_rows.Max(item => item.LeadingToken?.Text.Length ?? 0)) },
                    new ColumnDefinition { Width = GridLength.Star() })
                .Rows(new RowDefinition { Height = GridLength.Auto })
                .ColumnGap(1)
                .Cell(Line(token.Text,
                    () => index == SelectedIndex ? token.SelectedStyle ?? token.Style : token.Style,
                    TextTrimming.EndEllipsis), 0, 0)
                .Cell(name, 0, 1)
                .HorizontalAlignment(Align.Stretch);
        }

        VStack stack = new VStack(name)
            .HorizontalAlignment(Align.Stretch);

        if (row.Meta is not null)
        {
            stack.Add(Line(
                row.Meta,
                () => row.HasContent
                    ? StraumrStyleService.AmberText
                    : index == SelectedIndex
                        ? StraumrStyleService.MutedBrightText
                        : StraumrStyleService.MutedText,
                TextTrimming.EndEllipsis));
        }

        if (row.Detail is not null)
        {
            stack.Add(Line(
                row.Detail,
                () => index == SelectedIndex
                    ? StraumrStyleService.MutedBrightText
                    : StraumrStyleService.MutedText,
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
