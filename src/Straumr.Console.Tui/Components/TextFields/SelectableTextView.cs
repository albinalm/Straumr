using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.TextFields;

internal sealed class SelectableTextView : TextView
{
    public SelectableTextView()
    {
        TabKeyAddsTab = false;
        EnterKeyAddsLine = false;
    }
    
    protected override bool OnKeyDown(Key key)
    {
        Rune rune = key.AsRune;

        if (!key.IsCtrl && !key.IsAlt)
        {
            if (rune.Value >= 32 || key == Key.Backspace || key == Key.DeleteChar || key == Key.Enter)
            {
                return true;
            }
        }

        return base.OnKeyDown(key);
    }
}
