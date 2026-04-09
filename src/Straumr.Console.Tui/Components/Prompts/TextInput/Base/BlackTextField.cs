using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.Prompts.TextInput.Base;

public class BlackTextField : TextField
{
    public BlackTextField()
    {
        var attr = new Attribute(Color.White, Color.Black);
        var scheme = new Scheme(attr)
        {
            Focus = new Attribute(attr),
            Editable = new  Attribute(attr),
            HotActive =  new  Attribute(attr),
        };

        SetScheme(scheme);
    }
}