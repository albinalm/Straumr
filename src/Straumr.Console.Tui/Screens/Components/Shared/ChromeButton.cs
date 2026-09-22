using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class ChromeButton : Button
{
    public ChromeButton(Visual content)
        : base(content)
    {
        Focusable = false;
    }
}
