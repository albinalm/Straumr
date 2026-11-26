using System.Diagnostics;
using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Styling;
using XenoAtom.Terminal.UI.Animation;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal sealed class FlowerSpinner : Visual, IAnimatedVisual
{
    private const double VerbSeconds = 2.6;
    private const string StillGlyph = "●";
    private static readonly Rune Blank = new(' ');
    private readonly Stopwatch _clock;
    private readonly Func<string?>? _hint;
    private readonly IReadOnlyList<string> _verbs;
    private long _next;

    public FlowerSpinner(IReadOnlyList<string> verbs, Stopwatch clock, Func<string?>? hint = null)
    {
        _verbs = verbs;
        _clock = clock;
        _hint = hint;
    }

    public static IReadOnlyList<string> SendVerbs { get; } =
    [
        "Casting the request", "Paddling upstream", "Riding the current", "Waiting on the wire", "Fishing for a response",
        "Testing the waters", "Navigating the rapids", "Rowing the packets", "Listening for headers", "Counting the ripples"
    ];

    public static IReadOnlyList<string> FetchVerbs { get; } =
        ["Fishing for a token", "Paddling upstream", "Waiting on the wire", "Reading the current"];

    public long NextAnimationTick => _clock.IsRunning ? _next : long.MaxValue;

    public bool AdvanceAnimation(long timestamp)
    {
        if (!_clock.IsRunning || timestamp < _next)
        {
            return false;
        }

        _next = timestamp + Stopwatch.Frequency / (StraumrStyleService.Motion.Spinner ? 18 : 10);
        return true;
    }

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        int natural = 2 + _verbs.Max(verb => verb.Length) + 1 + 12 + (_hint?.Invoke()?.Length + 3 ?? 0);
        return SizeHints.Flex(new Size(8, 1), new Size(natural, 1), new Size(natural, 1), 0, 0, 1, 0);
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        Rectangle bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        int y = bounds.Y;
        Style ground = Style.None.WithBackground(StraumrStyleService.Background);
        for (int x = bounds.X; x < bounds.Right; x++)
        {
            buffer.SetCell(x, y, Blank, ground);
        }

        bool moving = StraumrStyleService.Motion.Spinner;
        double seconds = _clock.Elapsed.TotalSeconds;
        IReadOnlyList<string> flower = StraumrStyleService.Flower;
        string glyph = moving ? flower[(int)(_clock.ElapsedMilliseconds / 110 % flower.Count)] : StillGlyph;
        int cursor = ChromeDrawing.Write(buffer, bounds, bounds.X, y, glyph, ChromeDrawing.Ink(StraumrStyleService.Accent, TextStyle.Bold));
        cursor++;

        string verb = moving ? _verbs[(int)(seconds / VerbSeconds) % _verbs.Count] : _verbs[0];
        double sweep = moving ? seconds * 14 % (verb.Length + 8) - 4 : double.NaN;
        for (int index = 0; index < verb.Length && cursor < bounds.Right; index++)
        {
            double distance = Math.Abs(index - sweep);
            Color color = distance < 3
                ? StraumrStyleService.Blend(StraumrStyleService.TextBright, StraumrStyleService.Accent, distance / 3)
                : StraumrStyleService.Accent;
            TextStyle textStyle = distance < 1 && !StraumrStyleService.Accent.IsRgbLike ? TextStyle.Bold : TextStyle.None;
            buffer.SetCell(cursor++, y, new Rune(verb[index]), ChromeDrawing.Ink(color, textStyle));
        }

        cursor = ChromeDrawing.Write(buffer, bounds, cursor, y, "…", ChromeDrawing.Ink(StraumrStyleService.Accent));
        string tail = _hint?.Invoke() is { Length: > 0 } hint ? $" ({seconds:0.0} s · {hint})" : $" ({seconds:0.0} s)";
        ChromeDrawing.Write(buffer, bounds, cursor, y, tail, ChromeDrawing.Faint(StraumrStyleService.Muted));
    }
}
