using System.Diagnostics;
using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Animation;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal sealed class PulseDot : Visual, IAnimatedVisual
{
    private static readonly long PulseTicks = Stopwatch.Frequency * 2;
    private static readonly long PhaseTicks = Stopwatch.Frequency / 4;
    private static readonly Rune Dot = new('●');
    private static readonly Rune Ring = new('○');
    private readonly Func<string?> _key;
    private readonly Func<TextBlockStyle> _style;
    private long _next;
    private string? _seen;
    private long _started = long.MinValue;

    public PulseDot(Func<TextBlockStyle> style, Func<string?> key)
    {
        _style = style;
        _key = key;
    }

    public long NextAnimationTick => Pulsing(Stopwatch.GetTimestamp()) ? _next : long.MaxValue;

    public bool AdvanceAnimation(long timestamp)
    {
        if (!Pulsing(timestamp) || timestamp < _next)
        {
            return false;
        }

        _next = timestamp + PhaseTicks / 2;
        return true;
    }

    protected override SizeHints MeasureCore(in LayoutConstraints constraints) => SizeHints.Fixed(new Size(1, 1));

    protected override void RenderOverride(CellBuffer buffer)
    {
        Rectangle bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        Color color = _style().Foreground ?? StraumrStyleService.Text;
        bool dim = Pulsing(now) && (now - _started) / PhaseTicks % 2 == 1;
        Rune glyph = dim && !color.IsRgbLike ? Ring : Dot;
        Color shown = dim ? StraumrStyleService.Blend(color, StraumrStyleService.Muted, 0.75) : color;
        buffer.SetCell(bounds.X, bounds.Y, glyph, ChromeDrawing.Ink(shown, TextStyle.Bold));
    }

    private bool Pulsing(long now)
    {
        if (!StraumrStyleService.Motion.Pulse)
        {
            return false;
        }

        string? key = _key();
        if (key != _seen)
        {
            _seen = key;
            _started = key is null ? long.MinValue : now;
            _next = now;
        }

        return _started != long.MinValue && now - _started < PulseTicks;
    }
}
