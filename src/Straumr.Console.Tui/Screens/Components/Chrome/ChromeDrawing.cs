using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Styling;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Rendering;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal static class ChromeDrawing
{
    public static int Write(CellBuffer buffer, Rectangle bounds, int x, int y, string text, Style style)
    {
        if (y < bounds.Y || y >= bounds.Bottom)
        {
            return x;
        }

        foreach (Rune rune in text.EnumerateRunes())
        {
            if (x >= bounds.Right)
            {
                break;
            }

            if (x >= bounds.X)
            {
                buffer.SetCell(x, y, rune, style);
            }

            x++;
        }

        return x;
    }

    public static Style Ink(Color color, TextStyle textStyle = TextStyle.None) =>
        Style.None.WithForeground(color).WithBackground(StraumrStyleService.Background).WithTextStyle(textStyle);

    public static Style Faint(Color color) =>
        StraumrStyleService.Tinted(color).WithBackground(StraumrStyleService.Background);
}
