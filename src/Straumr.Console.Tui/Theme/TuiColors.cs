namespace Straumr.Console.Tui.Theme;

using Terminal.Gui.Drawing;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

public static class TuiColors
{
    public static Color Resolve(string value)
    {
        if (value.StartsWith('#'))
        {
            string hex = value.TrimStart('#');
            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex[2..4], 16);
            var b = Convert.ToInt32(hex[4..6], 16);
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

    public static Scheme BuildScheme(TuiTheme theme)
    {
        Color bg = Resolve(theme.Background);
        Color fg = Resolve(theme.Foreground);
        Color accent = Resolve(theme.Accent);
        Color muted = Resolve(theme.Muted);

        return new Scheme(new TuiAttribute(fg, bg))
        {
            Focus = new TuiAttribute(fg, bg),
            HotNormal = new TuiAttribute(accent, bg),
            HotFocus = new TuiAttribute(accent, bg),
            Disabled = new TuiAttribute(muted, bg),
        };
    }

    public static Scheme BuildListScheme(TuiTheme theme)
    {
        Color bg = Resolve(theme.Background);
        Color fg = Resolve(theme.Foreground);
        Color accent = Resolve(theme.Accent);
        Color selBg = Resolve(theme.SelectionBackground);
        Color muted = Resolve(theme.Muted);

        return new Scheme(new TuiAttribute(fg, bg))
        {
            Focus = new TuiAttribute(accent, selBg),
            HotNormal = new TuiAttribute(accent, bg),
            HotFocus = new TuiAttribute(bg, accent),
            Disabled = new TuiAttribute(muted, bg),
        };
    }
}
