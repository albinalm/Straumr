using Straumr.Console.Tui.Components.Base;
using Straumr.Console.Tui.Theme;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components;

public class Banner : TuiComponent
{
    public Pos X { get; init; } = 0;
    public Pos Y { get; init; } = 0;
    public TuiTheme? Theme { get; init; }

    public override View Build()
    {
        var label = new Label
        {
            Text = Branding.Figlet,
            X = X,
            Y = Y,
        };

        TuiTheme theme = Theme ?? new TuiTheme();
        Color fg = TuiColors.Resolve(theme.Accent);
        Color bg = TuiColors.Resolve(theme.Background);

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
