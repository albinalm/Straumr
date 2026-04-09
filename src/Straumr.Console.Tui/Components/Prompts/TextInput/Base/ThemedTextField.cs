using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.Prompts.TextInput.Base;

internal class ThemedTextField : TextField
{
    internal ThemedTextField(Color background, Color foreground)
    {
        var attr = new Attribute(foreground, background);
        var scheme = new Scheme(attr)
        {
            Focus = new Attribute(attr),
            Editable = new  Attribute(attr),
            HotActive =  new  Attribute(attr),
        };

        SetScheme(scheme);
    }
}