using System.Text;
using Straumr.Console.Shared.Helpers;
using Straumr.Core.Enums;
using Straumr.Core.Models;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Scrolling;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Visuals;

public sealed partial class RequestList : Visual, IScrollable
{
    private const int ItemHeight = 3;
    private const int ItemSpacing = 1;
    private const int ItemStride = ItemHeight + ItemSpacing;
    private const int MarkerWidth = 2;

    private static readonly TextBlockStyle MutedTextStyle =
        TextBlockStyle.Default with { Foreground = Colors.Gray };

    private readonly IReadOnlyList<StraumrRequest> _requests;
    private readonly IReadOnlyList<Visual> _items;
    private readonly ScrollModel _scroll;

    public RequestList(IEnumerable<StraumrRequest> requests)
    {
        _requests = requests.ToArray();
        _items = _requests.Select(BuildItem).ToArray();
        _scroll = new ScrollModel(this);
        SelectedIndex = _requests.Count == 0 ? -1 : 0;

        foreach (var item in _items)
            AttachChild(item);

        Focusable = true;
        HorizontalAlignment = Align.Stretch;
        VerticalAlignment = Align.Stretch;
    }

    public ScrollModel Scroll => _scroll;

    [Bindable]
    public partial int SelectedIndex { get; set; }

    public StraumrRequest? SelectedRequest =>
        SelectedIndex < 0 ? null : _requests[SelectedIndex];

    protected override int ChildrenCount => _items.Count;

    protected override Visual GetChild(int index) => _items[index];

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        var contentWidth = 0;
        var itemConstraints = new LayoutConstraints(
            0,
            LayoutConstraints.Unbounded.MaxWidth,
            0,
            ItemHeight);

        foreach (var item in _items)
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

        var contentWidth = Math.Max(0, finalRect.Width - MarkerWidth);
        var extentWidth = Math.Max(finalRect.Width, DesiredSize.Width);

        _scroll.SetViewport(finalRect.Width, finalRect.Height);
        _scroll.SetExtent(extentWidth, ContentHeight);
        EnsureSelectedVisible();

        for (var index = 0; index < _items.Count; index++)
        {
            var y = finalRect.Y + index * ItemStride - _scroll.OffsetY;
            _items[index].Arrange(
                new Rectangle(
                    finalRect.X + MarkerWidth - _scroll.OffsetX,
                    y,
                    contentWidth,
                    ItemHeight));
        }
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        var bounds = Bounds;
        var listStyle = GetStyle(ListBoxStyle.Key);
        var theme = GetTheme();

        for (var index = 0; index < _items.Count; index++)
        {
            var top = bounds.Y + index * ItemStride - _scroll.OffsetY;
            var bottom = top + ItemHeight;

            if (bottom <= bounds.Y || top >= bounds.Bottom)
                continue;

            var selected = index == SelectedIndex;
            var style = listStyle.ResolveItemStyle(
                theme,
                IsEnabled,
                selected,
                HasFocus);

            for (var y = Math.Max(top, bounds.Y); y < Math.Min(bottom, bounds.Bottom); y++)
            {
                for (var x = bounds.X; x < bounds.Right; x++)
                    buffer.SetCell(x, y, new Rune(' '), style);
            }

            if (selected && top >= bounds.Y && top < bounds.Bottom)
                buffer.SetCell(bounds.X, top, listStyle.MarkerGlyph, style);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_requests.Count == 0)
            return;

        var pageSize = Math.Max(1, Bounds.Height / ItemStride);

        switch (e.Key)
        {
            case TerminalKey.Up:
                SelectedIndex--;
                break;
            case TerminalKey.Down:
                SelectedIndex++;
                break;
            case TerminalKey.Home:
                SelectedIndex = 0;
                break;
            case TerminalKey.End:
                SelectedIndex = _requests.Count - 1;
                break;
            case TerminalKey.PageUp:
                SelectedIndex -= pageSize;
                break;
            case TerminalKey.PageDown:
                SelectedIndex += pageSize;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerEventArgs e)
    {
        if (e.Button != TerminalMouseButton.Left)
            return;

        var row = e.UiY - Bounds.Y + _scroll.OffsetY;
        var index = row / ItemStride;

        if ((uint)index >= (uint)_requests.Count || row % ItemStride >= ItemHeight)
            return;

        SelectedIndex = index;
        e.Handled = true;
    }

    protected override void OnPointerWheel(PointerEventArgs e)
    {
        if (_requests.Count == 0 || e.WheelDelta == 0)
            return;

        SelectedIndex += e.WheelDelta > 0 ? -1 : 1;
        e.Handled = true;
    }

    private void EnsureSelectedVisible()
    {
        if (SelectedIndex < 0 || _scroll.ViewportHeight <= 0)
            return;

        var itemTop = SelectedIndex * ItemStride;
        var itemBottom = itemTop + ItemHeight;
        var viewportBottom = _scroll.OffsetY + _scroll.ViewportHeight;

        if (itemTop < _scroll.OffsetY)
            _scroll.SetOffset(_scroll.OffsetX, itemTop);
        else if (itemBottom > viewportBottom)
            _scroll.SetOffset(
                _scroll.OffsetX,
                itemBottom - _scroll.ViewportHeight);
    }

    partial void OnSelectedIndexChanging(ref int value)
    {
        value = _requests.Count == 0
            ? -1
            : Math.Clamp(value, 0, _requests.Count - 1);
    }

    partial void OnSelectedIndexChanged(int value)
    {
        EnsureSelectedVisible();
    }

    private int ContentHeight =>
        _items.Count == 0
            ? 0
            : _items.Count * ItemStride - ItemSpacing;

    private static Visual BuildItem(StraumrRequest request)
    {
        var body = request.BodyType == BodyType.None
            ? "No body"
            : $"{RequestEditingHelpers.BodyTypeDisplayName(request.BodyType)} body";

        var auth = request.AuthId.HasValue ? "Auth" : "No auth";

        return new VStack(
                new HStack(
                        new TextBlock(request.Method.Method)
                            .Style(MethodStyle(request.Method)),
                        new TextBlock(request.Name))
                    .Spacing(1),
                new TextBlock(request.Id.ToString())
                    .Style(MutedTextStyle),
                new HStack(
                        new TextBlock(request.Uri),
                        new TextBlock($"· {body} · {auth}")
                            .Style(MutedTextStyle),
                        new TextBlock(request.LastAccessed.ToString("yyyy-MM-dd"))
                            .Style(TextBlockStyle.Default with { Foreground = Colors.Blue }))
                    .Spacing(1))
            .HorizontalAlignment(Align.Stretch);
    }

    private static TextBlockStyle MethodStyle(HttpMethod method)
    {
        var color = method.Method.ToUpperInvariant() switch
        {
            "GET" => Colors.Green,
            "POST" => Colors.Blue,
            "PUT" => Colors.Yellow,
            "PATCH" => Colors.Cyan,
            "DELETE" => Colors.Red,
            _ => Colors.Gray
        };

        return TextBlockStyle.Default with { Foreground = color };
    }
}
