using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.TextFields;

internal sealed class InteractiveTextView : TextView
{
    public void ApplyTheme(Color background, Color foreground)
    {
        var attr = new Attribute(foreground, background);
        Scheme scheme = new(attr)
        {
            Normal = new Attribute(attr),
            Focus = new Attribute(attr),
            Editable = new Attribute(attr),
            ReadOnly = new Attribute(attr),
            Disabled = new Attribute(attr),
            HotNormal = new Attribute(attr),
            HotFocus = new Attribute(attr)
        };

        SetScheme(scheme);
    }
}
