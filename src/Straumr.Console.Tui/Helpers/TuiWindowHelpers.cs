using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Helpers;

internal static class TuiWindowHelpers
{
    private static TerminalApp? _app;

    public static void AttachTo(TerminalApp app) => _app = app;

    public static void Show(Dialog dialog, Action? shown = null)
    {
        if (_app is not { } app)
        {
            dialog.Show();
            shown?.Invoke();
            return;
        }

        // Showing during the pointer pass can bury the new dialog beneath its opener.
        app.Post(() =>
        {
            dialog.Show();
            shown?.Invoke();
        });
    }
}
