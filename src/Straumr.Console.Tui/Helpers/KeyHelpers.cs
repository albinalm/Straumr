using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Helpers;

internal static class KeyHelpers
{
    private const KeyCode ModifierMask = KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask;

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

    // Terminal.Gui's built-in text input reads Key.AsRune, which on the ANSI-driver path (Kitty/
    // Alacritty) holds the unshifted character. If ShiftedKeyCode differs, rebuild the Key from
    // ShiftedKeyCode so AsRune lines up with what the user actually typed. Key.KeyCode is init-only,
    // so we construct a fresh Key instead of mutating.
    internal static Key NormalizeForTextInput(Key key)
    {
        KeyCode sk = key.ShiftedKeyCode;
        if (sk == KeyCode.Null || (int)sk == key.AsRune.Value)
        {
            return key;
        }

        return new Key(sk);
    }

    internal static bool IsEscape(Key key) => StripModifiers(key.KeyCode) == KeyCode.Esc;

    internal static bool IsEnter(Key key) => StripModifiers(key.KeyCode) == KeyCode.Enter;

    internal static bool IsTabForward(Key key)
    {
        KeyCode baseKey = StripModifiers(key.KeyCode);
        return (baseKey == KeyCode.Tab && !key.IsShift)
               || (!key.IsAlt && !key.IsShift && key.IsCtrl && key.AsRune.Value == 9);
    }

    internal static bool IsTabBackward(Key key)
    {
        KeyCode baseKey = StripModifiers(key.KeyCode);
        return baseKey == KeyCode.Tab && key.IsShift;
    }

    internal static bool IsTabNavigation(Key key) => IsTabForward(key) || IsTabBackward(key);

    internal static bool IsCursorUp(Key key) => StripModifiers(key.KeyCode) == KeyCode.CursorUp;

    internal static bool IsCursorDown(Key key) => StripModifiers(key.KeyCode) == KeyCode.CursorDown;

    private static KeyCode StripModifiers(KeyCode keyCode) => keyCode & ~ModifierMask;
}
