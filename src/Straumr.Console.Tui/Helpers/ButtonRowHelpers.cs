using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Helpers;

internal static class ButtonRowHelpers
{
    public static void Hover(params Button[] buttons)
    {
        foreach (Button button in buttons)
        {
            button.PointerMoved(() =>
            {
                if (!button.HasFocus && button.IsEnabled && button.IsReachable())
                {
                    button.App?.Focus(button);
                }
            });
        }
    }
}
