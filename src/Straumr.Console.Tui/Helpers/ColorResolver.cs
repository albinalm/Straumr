using Straumr.Console.Shared.Theme;
using Terminal.Gui.Drawing;

namespace Straumr.Console.Tui.Helpers;

using TuiAttribute = Terminal.Gui.Drawing.Attribute;

public static class ColorResolver
{
    public static Color Resolve(string value)
    {
        if (value.StartsWith('#'))
        {
            string hex = value.TrimStart('#');
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);
            return new Color(r, g, b);
        }

        return value switch
        {
            "Black" => Color.Black,
            "Blue" => Color.Blue,
            "Green" => Color.Green,
            "Cyan" => Color.Cyan,
            "Red" => Color.Red,
            "Magenta" => Color.Magenta,
            "Yellow" => Color.Yellow,
            "Gray" => Color.Gray,
            "DarkGray" => Color.DarkGray,
            "BrightBlue" => Color.BrightBlue,
            "BrightGreen" => Color.BrightGreen,
            "BrightCyan" => Color.BrightCyan,
            "BrightRed" => Color.BrightRed,
            "BrightMagenta" => Color.BrightMagenta,
            "BrightYellow" => Color.BrightYellow,
            _ => Color.White
        };
    }

    public static Scheme BuildScheme(StraumrTheme theme)
    {
        Color surface = Resolve(theme.Surface);
        Color onSurface = Resolve(theme.OnSurface);
        Color accent = Resolve(theme.Accent);
        Color secondary = Resolve(theme.Secondary);

        return new Scheme(new TuiAttribute(onSurface, surface))
        {
            Focus = new TuiAttribute(onSurface, surface),
            HotNormal = new TuiAttribute(accent, surface),
            HotFocus = new TuiAttribute(accent, surface),
            Disabled = new TuiAttribute(secondary, surface),
        };
    }

    public static Scheme BuildListScheme(StraumrTheme theme)
    {
        Color surface = Resolve(theme.Surface);
        Color onSurface = Resolve(theme.OnSurface);
        Color primary = Resolve(theme.Primary);
        Color onPrimary = Resolve(theme.OnPrimary);
        Color surfaceVariant = Resolve(theme.SurfaceVariant);
        Color accent = Resolve(theme.Accent);
        Color secondary = Resolve(theme.Secondary);

        return new Scheme(new TuiAttribute(onSurface, surface))
        {
            Focus = new TuiAttribute(primary, surfaceVariant),
            HotNormal = new TuiAttribute(accent, surface),
            HotFocus = new TuiAttribute(onPrimary, primary),
            Disabled = new TuiAttribute(secondary, surface),
        };
    }

    public static Scheme BuildButtonScheme(StraumrTheme theme)
    {
        Color surface = Resolve(theme.Surface);
        Color onSurface = Resolve(theme.OnSurface);
        Color primary = Resolve(theme.Primary);
        Color onPrimary = Resolve(theme.OnPrimary);
        Color secondary = Resolve(theme.Secondary);

        return new Scheme(new TuiAttribute(onSurface, surface))
        {
            Focus = new TuiAttribute(onPrimary, primary),
            HotNormal = new TuiAttribute(primary, surface),
            HotFocus = new TuiAttribute(onPrimary, primary),
            Disabled = new TuiAttribute(secondary, surface),
        };
    }

}
