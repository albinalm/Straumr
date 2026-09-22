namespace Straumr.Core.Configuration;

public readonly record struct StraumrKeyGesture(char? Character, string? Key, ConsoleModifiers Modifiers)
{
    private static readonly HashSet<string> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Enter", "Escape", "Tab", "Backspace", "Delete", "Insert", "Home", "End",
        "Up", "Down", "Left", "Right", "PageUp", "PageDown",
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
    };

    public char? Echo => (Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0 ? Character : null;

    public static bool TryParse(string? text, out StraumrKeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string remaining = text.Trim();
        ConsoleModifiers modifiers = 0;
        while (remaining.Length > 1 && remaining.IndexOf('+') is > 0 and var separator)
        {
            ConsoleModifiers modifier = remaining[..separator].Trim().ToLowerInvariant() switch
            {
                "ctrl" or "control" => ConsoleModifiers.Control,
                "alt" => ConsoleModifiers.Alt,
                "shift" => ConsoleModifiers.Shift,
                _ => 0
            };
            if (modifier == 0 || (modifiers & modifier) != 0)
            {
                return false;
            }

            modifiers |= modifier;
            remaining = remaining[(separator + 1)..].Trim();
        }

        if (remaining.Equals("Space", StringComparison.OrdinalIgnoreCase))
        {
            remaining = " ";
        }

        if (remaining.Equals("Plus", StringComparison.OrdinalIgnoreCase))
        {
            remaining = "+";
        }

        if (remaining.Length == 1 && !char.IsControl(remaining[0]) && !char.IsSurrogate(remaining[0]))
        {
            char character = remaining[0];
            if ((modifiers & ConsoleModifiers.Shift) != 0)
            {
                character = char.ToUpperInvariant(character);
            }

            if ((modifiers & ConsoleModifiers.Control) != 0 && char.IsAsciiLetter(character))
            {
                character = char.ToUpperInvariant(character);
            }

            gesture = new StraumrKeyGesture(character, null, modifiers);
            return true;
        }

        if (remaining.Equals("Esc", StringComparison.OrdinalIgnoreCase))
        {
            remaining = "Escape";
        }

        if (remaining.Equals("Return", StringComparison.OrdinalIgnoreCase))
        {
            remaining = "Enter";
        }

        string? key = NamedKeys.FirstOrDefault(name => name.Equals(remaining, StringComparison.OrdinalIgnoreCase));
        if (key is null)
        {
            return false;
        }

        gesture = new StraumrKeyGesture(null, key, modifiers);
        return true;
    }

    public bool Matches(char character, string key, ConsoleModifiers modifiers)
    {
        if (Character is not { } expected)
        {
            return Key == key && Modifiers == modifiers;
        }

        if ((Modifiers & ~ConsoleModifiers.Shift) != (modifiers & ~ConsoleModifiers.Shift))
        {
            return false;
        }

        // Terminals may encode Ctrl+letter as a character, control byte, or named key.
        if ((Modifiers & ConsoleModifiers.Control) != 0 && char.IsAsciiLetter(expected))
        {
            char upper = char.ToUpperInvariant(expected);
            return Modifiers == modifiers &&
                   (char.ToUpperInvariant(character) == upper || character == (upper & 0x1f) ||
                    key.Length == 1 && char.ToUpperInvariant(key[0]) == upper);
        }
        return expected == character;
    }

    public bool Matches(ConsoleKeyInfo key) => Matches(key.KeyChar, key.Key switch
    {
        ConsoleKey.UpArrow => "Up",
        ConsoleKey.DownArrow => "Down",
        ConsoleKey.LeftArrow => "Left",
        ConsoleKey.RightArrow => "Right",
        _ => key.Key.ToString()
    }, key.Modifiers);

    public override string ToString() =>
        ((Modifiers & ConsoleModifiers.Control) != 0 ? "Ctrl+" : "") +
        ((Modifiers & ConsoleModifiers.Alt) != 0 ? "Alt+" : "") +
        ((Modifiers & ConsoleModifiers.Shift) != 0 ? "Shift+" : "") +
        (Character is ' ' ? "Space" : Character?.ToString() ?? Key);
}
