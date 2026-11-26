using System.Diagnostics;
using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Animation;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal sealed class NetworkBand : Visual, IAnimatedVisual
{
    private const string Wave = "~~";
    private const string SegmentGap = "   ";
    private const int TaglineFloor = 36;
    private const int SparkWidth = 10;
    private const int RateDigits = 3;
    private static readonly long TypeTicks = Stopwatch.Frequency * 28 / 1000;
    private static readonly string[] RateUnits = ["B/s", "KiB/s", "MiB/s", "GiB/s"];
    private static readonly Rune[] Blocks = [new('▁'), new('▂'), new('▃'), new('▄'), new('▅'), new('▆'), new('▇'), new('█')];
    private static readonly Rune Blank = new(' ');
    private long _next;

    public NetworkBand() => HorizontalAlignment = Align.Stretch;

    public long NextAnimationTick => _next;

    public bool AdvanceAnimation(long timestamp)
    {
        if (timestamp < _next)
        {
            return false;
        }

        long next = timestamp + Stopwatch.Frequency;
        (string line, long since) = ChromeTaglines.Current;
        if (StraumrStyleService.Motion.Transitions && Typed(since, timestamp) < line.Length)
        {
            next = Math.Min(next, timestamp + TypeTicks);
        }

        _next = next;
        return true;
    }

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        (string line, _) = ChromeTaglines.Current;
        int natural = 2 * (Wave.Length + 1) + line.Length + 48;
        return SizeHints.Flex(new Size(0, 1), new Size(natural, 1), new Size(LayoutConstraints.Unbounded.MaxWidth, 1), 1, 0, 1, 0);
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

        (string line, long since) = ChromeTaglines.Current;
        int reserved = 2 * (Wave.Length + 1) + Math.Min(line.Length, TaglineFloor) + SegmentGap.Length;
        List<(string Text, Style Style)> stats = Fit(NetworkStatsService.Snapshot(), Math.Max(0, bounds.Width - reserved));
        int statsWidth = stats.Sum(run => run.Text.Length);
        int statsStart = bounds.Right - statsWidth;
        int limit = statsWidth > 0 ? statsStart - SegmentGap.Length : bounds.Right;

        Style wave = ChromeDrawing.Ink(StraumrStyleService.Section);
        int cursor = ChromeDrawing.Write(buffer, bounds, bounds.X, y, Wave, wave) + 1;
        int room = Math.Max(0, limit - cursor - Wave.Length - 1);
        string shown = line.Length <= room ? line : room <= 1 ? string.Empty : line[..(room - 1)] + "…";
        int typed = StraumrStyleService.Motion.Transitions ? Typed(since, Stopwatch.GetTimestamp()) : int.MaxValue;
        cursor = ChromeDrawing.Write(buffer, bounds, cursor, y, shown[..Math.Min(shown.Length, typed)], ChromeDrawing.Ink(StraumrStyleService.Section, TextStyle.Bold));
        if (typed >= shown.Length)
        {
            ChromeDrawing.Write(buffer, bounds, cursor + 1, y, Wave, wave);
        }

        int x0 = statsStart;
        foreach ((string text, Style style) in stats)
        {
            x0 = ChromeDrawing.Write(buffer, bounds, x0, y, text, style);
        }
    }

    private static int Typed(long since, long now) => (int)Math.Max(0, (now - since) / TypeTicks);

    private static List<(string Text, Style Style)> Fit(NetworkStatsModel stats, int width)
    {
        if (!stats.HasTraffic)
        {
            return [];
        }

        (bool Up, bool Sparks)[] variants = [(true, true), (true, false), (false, true), (false, false)];
        foreach ((bool up, bool sparks) in variants)
        {
            List<(string Text, Style Style)> runs = Build(stats, up, sparks);
            if (runs.Sum(run => run.Text.Length) <= width)
            {
                return runs;
            }
        }

        return [];
    }

    private static List<(string Text, Style Style)> Build(NetworkStatsModel stats, bool up, bool sparks)
    {
        List<(string Text, Style Style)> runs = [];
        Style label = ChromeDrawing.Faint(StraumrStyleService.Muted);
        Style value = ChromeDrawing.Ink(StraumrStyleService.MutedBright);

        void Traffic(string arrow, double rate, IReadOnlyList<double> history)
        {
            if (runs.Count > 0)
            {
                runs.Add((SegmentGap, label));
            }

            (string number, string unit) = Rate(rate);
            runs.Add((new string(' ', Math.Max(0, RateDigits - number.Length)) + arrow + " ", label));
            runs.Add(($"{number} {unit,-5}", value));
            if (sparks)
            {
                runs.Add((" ", label));
                runs.AddRange(Sparkline(history, SparkWidth));
            }
        }

        Traffic("↓", stats.Down, stats.DownHistory);
        if (up)
        {
            Traffic("↑", stats.Up, stats.UpHistory);
        }

        return runs;
    }

    private static IEnumerable<(string Text, Style Style)> Sparkline(IReadOnlyList<double> values, int width)
    {
        double peak = values.Count == 0 ? 0 : values.Max();
        int padding = Math.Max(0, width - values.Count);
        Style track = StraumrStyleService.GaugeTrack.WithBackground(StraumrStyleService.Background);
        for (int index = 0; index < padding; index++)
        {
            yield return (Blocks[0].ToString(), track);
        }

        foreach (double sample in values.Skip(Math.Max(0, values.Count - width)))
        {
            if (peak <= 0 || sample <= 0)
            {
                yield return (Blocks[0].ToString(), track);
                continue;
            }

            double level = sample / peak;
            int block = Math.Clamp((int)Math.Ceiling(level * Blocks.Length) - 1, 0, Blocks.Length - 1);
            yield return (Blocks[block].ToString(), ChromeDrawing.Ink(StraumrStyleService.RampColor(level)));
        }
    }

    private static (string Number, string Unit) Rate(double bytesPerSecond)
    {
        double value = Math.Max(0, bytesPerSecond);
        int unit = 0;
        while (value >= 1000 && unit < RateUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string number = unit == 0 ? $"{value:0}" : value < 10 ? $"{value:0.0}" : $"{value:0}";
        return (number, RateUnits[unit]);
    }

}
