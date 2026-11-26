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
                if (!button.HasFocus && Reachable(buttons).Contains(button))
                {
                    button.App?.Focus(button);
                }
            });
        }
    }

    private static Button[] Reachable(Button[] buttons) =>
        buttons.Where(button => button.IsVisible && button.IsEnabled && button.IsReachable()).ToArray();
}
