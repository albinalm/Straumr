using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Base;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components;

public class Banner : TuiComponent
{
    public Pos X { get; init; } = 0;
    public Pos Y { get; init; } = 0;
    public StraumrTheme? Theme { get; init; }

    public override View Build()
    {
        var label = new Label
        {
            Text = Branding.Figlet,
            X = X,
            Y = Y,
        };

        StraumrTheme theme = Theme ?? new StraumrTheme();
        Color fg = ColorResolver.Resolve(theme.Primary);
        Color bg = ColorResolver.Resolve(theme.Surface);

        var scheme = new Scheme(new TuiAttribute(fg, bg))
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
