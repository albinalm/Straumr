using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal sealed class JunctionRule : Visual
{
    private const int Gap = 1;
    private static readonly Rune Line = new('─');
    private Visual? _endLabel;
    private Visual? _startLabel;

    public JunctionRule()
    {
        HorizontalAlignment = Align.Stretch;
        ChromeJunctions.Add(this);
    }

    public Visual? StartLabel
    {
        get => _startLabel;
        set => Replace(ref _startLabel, value);
    }

    public Visual? EndLabel
    {
        get => _endLabel;
        set => Replace(ref _endLabel, value);
    }

    public Style LineStyle => GetStyle<RuleStyle>().LineStyle ?? StraumrStyleService.DividerCell;

    protected override int ChildrenCount => (_startLabel is null ? 0 : 1) + (_endLabel is null ? 0 : 1);

    public bool IsLine(int x, int y)
    {
        Rectangle bounds = Bounds;
        if (y != bounds.Y || x < bounds.X || x >= bounds.Right)
        {
            return false;
        }

        return !Covers(_startLabel, x) && !Covers(_endLabel, x);
    }

    protected override Visual GetChild(int index) => index switch
    {
        0 when _startLabel is not null => _startLabel,
        0 or 1 when _endLabel is not null => _endLabel,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        int available = constraints.IsWidthBounded ? Math.Max(0, constraints.MaxWidth) : int.MaxValue / 4;
        int start = MeasureLabel(_startLabel, available);
        int end = MeasureLabel(_endLabel, Math.Max(0, available - start - 2 * Gap - 1));
        int natural = start + end + 4 * Gap + 2;
        return SizeHints.Flex(new Size(0, 1), new Size(natural, 1), new Size(LayoutConstraints.Unbounded.MaxWidth, 1), 1, 0, 1, 0);
    }

    protected override void ArrangeCore(in Rectangle finalRect)
    {
        int start = Math.Min(finalRect.Width, Natural(_startLabel));
        _startLabel?.Arrange(new Rectangle(finalRect.X, finalRect.Y, start, 1));
        int room = Math.Max(0, finalRect.Width - start - 2 * Gap - 1);
        int end = Math.Min(room, Natural(_endLabel));
        _endLabel?.Arrange(new Rectangle(finalRect.Right - end, finalRect.Y, end, 1));
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        Rectangle bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        int y = bounds.Y;
        Style style = LineStyle;
        for (int x = bounds.X; x < bounds.Right; x++)
        {
            if (IsLine(x, y))
            {
                buffer.SetCell(x, y, Line, style);
            }
        }

        foreach (JunctionDivider divider in ChromeJunctions.DividersNear(this))
        {
            int x = divider.Bounds.X;
            if (!IsLine(x, y))
            {
                continue;
            }

            bool up = ChromeJunctions.Contains(divider, x, y - 1);
            bool down = ChromeJunctions.Contains(divider, x, y + 1);
            if (up || down)
            {
                buffer.SetCell(x, y, ChromeJunctions.Glyph(up, down, IsLine(x - 1, y), IsLine(x + 1, y)), style);
            }
        }
    }

    private static int MeasureLabel(Visual? label, int available)
    {
        if (label is null || !label.IsVisible)
        {
            return 0;
        }

        label.Measure(new LayoutConstraints(0, Math.Max(0, available), 1, 1));
        return Math.Min(available, label.DesiredSize.Width);
    }

    private static int Natural(Visual? label) => label is null || !label.IsVisible ? 0 : label.DesiredSize.Width;

    private static bool Covers(Visual? label, int x)
    {
        if (label is null || !label.IsVisible || label.Bounds.Width <= 0)
        {
            return false;
        }

        Rectangle bounds = label.Bounds;
        return x >= bounds.X - Gap && x < bounds.Right + Gap;
    }

    private void Replace(ref Visual? field, Visual? value)
    {
        if (ReferenceEquals(field, value))
        {
            return;
        }

        if (field is not null)
        {
            DetachChild(field);
        }

        field = value;
        if (value is not null)
        {
            AttachChild(value);
        }
    }
}
