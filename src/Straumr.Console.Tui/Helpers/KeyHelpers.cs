using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Helpers;

internal static class KeyHelpers
{
    // In native AOT, Terminal.Gui's keyboard layout translation (RequiresDynamicCode) fails silently,
    // so AsRune returns the raw key code rather than the actual typed character. Non-letter keys that
    // require Shift on non-US keyboards (e.g. Shift+7='/' on Swedish) are affected. ShiftedKeyCode is
    // populated by terminals using the Kitty keyboard protocol and carries the correct typed character,
    // so it takes priority when available.
    internal static int GetCharValue(Key key)
    {
        var shiftedKc = (int)key.ShiftedKeyCode;
        return shiftedKc != 0 ? shiftedKc : key.AsRune.Value;
    }
}
