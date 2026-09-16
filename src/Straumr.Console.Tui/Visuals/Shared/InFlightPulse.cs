using System.Diagnostics;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Animation;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// What a bar shows while the app is waiting on the network: the compact pulse and the time it has
/// been waiting, ticking. It is a bar's right half, opposite whatever identifies the thing in
/// flight, and it gives the row back to that thing's result when the wait ends.
/// </summary>
/// <remarks>
/// The label needs the framework's animation scheduler rather than the screen's update pass, because
/// the update pass is itself awaiting the operation being timed: driven from there the duration froze
/// at the moment the send started, which is exactly when it is worth reading. The scheduler keeps
/// ticking while the pass is awaited, and asks for no tick at all once the clock stops.
/// </remarks>
internal static class InFlightPulse
{
    /// <param name="label">
    /// What the wait is called, in capitals as the bar's other states are — <c>IN FLIGHT</c> for a
    /// request, <c>FETCHING</c> for a token.
    /// </param>
    /// <param name="clock">
    /// The caller's clock, so the pulse times the operation rather than its own construction, and
    /// stops when the caller stops it.
    /// </param>
    public static Visual Create(string label, Stopwatch clock) =>
        new HStack(
                new Spinner().Style(StraumrStyles.RequestPulse),
                new ElapsedLabel(label, clock))
            .Spacing(2);

    private sealed class ElapsedLabel : Visual, IAnimatedVisual
    {
        private readonly string _label;
        private readonly Stopwatch _clock;
        private readonly TextBlock _text;
        private long _nextTick;

        public ElapsedLabel(string label, Stopwatch clock)
        {
            _label = label;
            _clock = clock;
            _text = new TextBlock(Format(TimeSpan.Zero)).Style(StraumrStyles.AccentText);
            AttachChild(_text);
        }

        protected override int ChildrenCount => 1;

        protected override Visual GetChild(int index) =>
            index == 0 ? _text : throw new ArgumentOutOfRangeException(nameof(index));

        protected override SizeHints MeasureCore(in LayoutConstraints constraints) =>
            _text.Measure(constraints);

        protected override void ArrangeCore(in Rectangle finalRect) => _text.Arrange(finalRect);

        public long NextAnimationTick => _clock.IsRunning ? _nextTick : long.MaxValue;

        public bool AdvanceAnimation(long timestamp)
        {
            if (!_clock.IsRunning || timestamp < _nextTick)
                return false;

            _nextTick = timestamp + Stopwatch.Frequency / 10;
            string text = Format(_clock.Elapsed);
            if (_text.Text == text)
                return false;

            _text.Text = text;
            return true;
        }

        private string Format(TimeSpan elapsed) => $"{_label} · {elapsed.TotalSeconds:0.0} s";
    }
}
