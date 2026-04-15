using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.TextFields;

internal sealed class InteractiveTextView : TextView
{
    protected override bool OnKeyDownNotHandled(Key key)
        => base.OnKeyDownNotHandled(KeyHelpers.NormalizeForTextInput(key));

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
