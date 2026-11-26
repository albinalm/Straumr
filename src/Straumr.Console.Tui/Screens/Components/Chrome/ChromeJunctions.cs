using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal static class ChromeJunctions
{
    private static readonly List<WeakReference<JunctionRule>> Rules = [];
    private static readonly List<WeakReference<JunctionDivider>> Dividers = [];

    public static void Add(JunctionRule rule) => Rules.Add(new WeakReference<JunctionRule>(rule));

    public static void Add(JunctionDivider divider) => Dividers.Add(new WeakReference<JunctionDivider>(divider));

    public static List<JunctionRule> RulesNear(Visual visual) => Live(Rules, visual);

    public static List<JunctionDivider> DividersNear(Visual visual) => Live(Dividers, visual);

    public static bool Contains(Visual visual, int x, int y)
    {
        Rectangle bounds = visual.Bounds;
        return x >= bounds.X && x < bounds.Right && y >= bounds.Y && y < bounds.Bottom;
    }

    public static Rune Glyph(bool up, bool down, bool left, bool right) => (up, down, left, right) switch
    {
        (true, true, true, true) => new Rune('┼'),
        (true, true, true, false) => new Rune('┤'),
        (true, true, false, true) => new Rune('├'),
        (false, true, true, true) => new Rune('┬'),
        (true, false, true, true) => new Rune('┴'),
        (false, true, true, false) => new Rune('┐'),
        (false, true, false, true) => new Rune('┌'),
        (true, false, true, false) => new Rune('┘'),
        (true, false, false, true) => new Rune('└'),
        (false, false, true, _) or (false, false, _, true) => new Rune('─'),
        _ => new Rune('│')
    };

    private static List<T> Live<T>(List<WeakReference<T>> registry, Visual near) where T : Visual
    {
        List<T> live = [];
        Visual layer = Layer(near);
        for (int index = registry.Count - 1; index >= 0; index--)
        {
            if (!registry[index].TryGetTarget(out T? visual))
            {
                registry.RemoveAt(index);
                continue;
            }

            if (!ReferenceEquals(visual, near) && visual.App is not null && ReferenceEquals(visual.App, near.App) &&
                Shown(visual) && ReferenceEquals(Layer(visual), layer))
            {
                live.Add(visual);
            }
        }

        return live;
    }

    private static bool Shown(Visual visual)
    {
        for (Visual? node = visual; node is not null; node = node.Parent)
        {
            if (!node.IsVisible)
            {
                return false;
            }
        }

        return true;
    }

    private static Visual Layer(Visual visual)
    {
        Visual node = visual;
        while (node.Parent is { } parent)
        {
            if (node is Dialog)
            {
                return node;
            }

            node = parent;
        }

        return node;
    }
}
