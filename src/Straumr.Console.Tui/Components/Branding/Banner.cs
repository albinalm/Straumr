using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Base;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.Branding;

public class Banner : TuiComponent
{
    private const string Figlet = """
                                      _
                                  ___| |_ _ __ __ _ _   _ _ __ ___  _ __
                                 / __| __| '__/ _` | | | | '_ ` _ \| '__|
                                 \__ \ |_| | | (_| | |_| | | | | | | |
                                 |___/\__|_|  \__,_|\__,_|_| |_| |_|_|

                                 """;

    public static readonly int FigletWidth = Figlet.Split('\n').Max(l => l.Length);
    public static readonly int FigletHeight = Figlet.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
    public Pos X { get; init; } = Pos.AnchorEnd(FigletWidth + 1);
    public Pos Y { get; init; } = 0;
    public StraumrTheme? Theme { get; init; }

    public override View Build()
    {
        Label label = new Label
        {
            Text = Figlet,
            X = X,
            Y = Y,
        };

        StraumrTheme theme = Theme ?? new StraumrTheme();
        Color fg = ColorResolver.Resolve(theme.Primary);
        Color bg = ColorResolver.Resolve(theme.Surface);

        Scheme scheme = new Scheme(new TuiAttribute(fg, bg))
        {
            Focus = new TuiAttribute(fg, bg),
            HotNormal = new TuiAttribute(fg, bg),
            HotFocus = new TuiAttribute(fg, bg),
            Disabled = new TuiAttribute(fg, bg),
        };

        label.SetScheme(scheme);
        return label;
    }
}
