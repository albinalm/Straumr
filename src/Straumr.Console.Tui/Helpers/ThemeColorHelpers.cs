using System.Globalization;
using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Helpers;

public static class ThemeColorHelpers
{
    private static readonly string[] BaseNames =
        ["black", "red", "green", "yellow", "blue", "magenta", "cyan", "white"];

    public static bool TryParse(string text, out Color color, out string? error)
    {
        color = Color.Default;
        error = null;
        string value = text.Trim();
        if (value.Length == 0)
        {
            error = "is empty";
            return false;
        }

        if (value[0] == '#')
        {
            return TryParseHex(value, out color, out error);
        }

        string name = value.Replace('_', '-').ToLowerInvariant();
        if (name is "default" or "terminal" or "inherit")
        {
            return true;
        }

        if (TryParseIndexed(name, out color, out error))
        {
            return error is null;
        }

        bool bright = false;
        if (name.StartsWith("bright-", StringComparison.Ordinal))
        {
            bright = true;
            name = name["bright-".Length..];
        }

        if (name == "purple")
        {
            name = "magenta";
        }

        int index = Array.IndexOf(BaseNames, name);
        if (index < 0)
        {
            error = $"'{text.Trim()}' is not a colour; use #rrggbb, default, a palette name such as " +
                    "blue or bright-black, or indexed:0-255";
            return false;
        }

        color = Color.Basic16(bright ? index + 8 : index);
        return true;
    }

    private static bool TryParseHex(string value, out Color color, out string? error)
    {
        color = Color.Default;
        error = null;
        ReadOnlySpan<char> digits = value.AsSpan(1);
        if (digits.Length == 3)
        {
            Span<char> expanded = stackalloc char[6];
            for (int index = 0; index < 3; index++)
            {
                expanded[index * 2] = expanded[index * 2 + 1] = digits[index];
            }

            digits = expanded.ToString();
        }

        if (digits.Length != 6 ||
            !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            error = $"'{value}' is not a hex colour; write #rrggbb or #rgb";
            return false;
        }

        color = Color.Rgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    private static bool TryParseIndexed(string name, out Color color, out string? error)
    {
        color = Color.Default;
        error = null;
        ReadOnlySpan<char> digits = name.StartsWith("indexed:", StringComparison.Ordinal)
            ? name.AsSpan("indexed:".Length)
            : name;
        if (digits.Length == 0 || !digits.ToString().All(char.IsAsciiDigit))
        {
            return false;
        }

        if (!int.TryParse(digits, out int index) || index is < 0 or > 255)
        {
            error = $"'{name}' is out of range; an indexed colour is 0-255";
            return true;
        }

        color = index < 16 ? Color.Basic16(index) : Color.Indexed256(index);
        return true;
    }
}
