using System.Diagnostics;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Animation;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class InFlightElapsedLabel : Visual, IAnimatedVisual
{
    private readonly Stopwatch _clock;
    private readonly string _label;
    private readonly TextBlock _text;
    private long _nextTick;

    public InFlightElapsedLabel(string label, Stopwatch clock)
    {
        _label = label;
        _clock = clock;
        _text = new TextBlock(Format(TimeSpan.Zero)).Style(StraumrStyleService.AccentText);
        AttachChild(_text);
    }

    protected override int ChildrenCount => 1;

    public long NextAnimationTick => _clock.IsRunning ? _nextTick : long.MaxValue;

    public bool AdvanceAnimation(long timestamp)
    {
        if (!_clock.IsRunning || timestamp < _nextTick)
        {
            return false;
        }

        _nextTick = timestamp + Stopwatch.Frequency / 10;
        string text = Format(_clock.Elapsed);
        if (_text.Text == text)
        {
            return false;
        }

        _text.Text = text;
        return true;
    }

    protected override Visual GetChild(int index) =>
        index == 0 ? _text : throw new ArgumentOutOfRangeException(nameof(index));

    protected override SizeHints MeasureCore(in LayoutConstraints constraints) =>
        _text.Measure(constraints);

    protected override void ArrangeCore(in Rectangle finalRect) => _text.Arrange(finalRect);

    private string Format(TimeSpan elapsed) => $"{_label} · {elapsed.TotalSeconds:0.0} s";
}
