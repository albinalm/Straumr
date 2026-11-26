using System.Diagnostics;
using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Animation;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal sealed class GaugeBar : Visual, IAnimatedVisual
{
    private static readonly long GrowTicks = Stopwatch.Frequency * 45 / 100;
    private static readonly Rune Filled = new('━');
    private static readonly Rune Track = new('─');
    private readonly double _ratio;
    private long _next;
    private long _started = -1;

    public GaugeBar(double ratio)
    {
        _ratio = double.IsFinite(ratio) ? Math.Clamp(ratio, 0, 1) : 0;
        HorizontalAlignment = Align.Stretch;
    }

    public long NextAnimationTick => Growing ? _next : long.MaxValue;

    private bool Growing => StraumrStyleService.Motion.Transitions && _started >= 0 && Stopwatch.GetTimestamp() - _started < GrowTicks;

    public bool AdvanceAnimation(long timestamp)
    {
        if (!Growing || timestamp < _next)
        {
            return false;
        }

        _next = timestamp + Stopwatch.Frequency / 30;
        return true;
    }

    protected override SizeHints MeasureCore(in LayoutConstraints constraints) =>
        SizeHints.Flex(new Size(4, 1), new Size(24, 1), new Size(LayoutConstraints.Unbounded.MaxWidth, 1), 1, 0, 1, 0);

    protected override void RenderOverride(CellBuffer buffer)
    {
        Rectangle bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        double shown = _ratio;
        if (StraumrStyleService.Motion.Transitions)
        {
            if (_started < 0)
            {
                _started = Stopwatch.GetTimestamp();
                _next = _started;
            }

            double progress = Math.Min(1, (Stopwatch.GetTimestamp() - _started) / (double)GrowTicks);
            shown *= 1 - Math.Pow(1 - progress, 3);
        }

        int width = bounds.Width;
        int filled = (int)Math.Round(width * shown);
        if (filled == 0 && _ratio > 0 && shown >= _ratio)
        {
            filled = 1;
        }

        Style track = StraumrStyleService.GaugeTrack.WithBackground(StraumrStyleService.Background);
        for (int index = 0; index < width; index++)
        {
            bool full = index < filled;
            buffer.SetCell(bounds.X + index, bounds.Y, full ? Filled : Track,
                full ? ChromeDrawing.Ink(StraumrStyleService.RampColor(width == 1 ? 0 : index / (double)(width - 1))) : track);
        }
    }
}
