using System.Collections.Concurrent;
using System.Globalization;
using Straumr.Console.Shared.Theme;
using Terminal.Gui.Drawing;

namespace Straumr.Console.Tui.Helpers;

using TuiAttribute = Terminal.Gui.Drawing.Attribute;

public static class ColorResolver
{
    private static readonly ConcurrentDictionary<string, Color> ResolveCache = new(StringComparer.Ordinal);

    public static Color Resolve(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Color.None;
        }

        return ResolveCache.TryGetValue(value, out Color cached)
            ? cached
            : ResolveCache.GetOrAdd(value, ResolveUncached);
    }

    private static Color ResolveUncached(string value)
    {
        string trimmed = value.Trim();

        if (trimmed.StartsWith('#'))
        {
            string hex = trimmed.TrimStart('#');
            if (hex.Length == 6)
            {
                var r = Convert.ToInt32(hex[..2], 16);
                var g = Convert.ToInt32(hex[2..4], 16);
                var b = Convert.ToInt32(hex[4..6], 16);
                return new Color(r, g, b);
            }

            if (hex.Length == 3)
            {
                string expanded = new string(new[]
                {
                    hex[0], hex[0],
                    hex[1], hex[1],
                    hex[2], hex[2]
                });
                var r = Convert.ToInt32(expanded[..2], 16);
                var g = Convert.ToInt32(expanded[2..4], 16);
                var b = Convert.ToInt32(expanded[4..6], 16);
                return new Color(r, g, b);
            }
        }

        if (Enum.TryParse(trimmed, true, out ColorName16 ansiColor))
        {
            return new Color(ansiColor);
        }

        if (Color.TryParse(trimmed, CultureInfo.InvariantCulture, out Color parsed))
        {
            return parsed;
        }

        return new Color(ColorName16.White);
    }

    public static bool ThemeHasTrueColor(StraumrTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        return HasTrueColor(theme.Surface)
               || HasTrueColor(theme.SurfaceVariant)
               || HasTrueColor(theme.OnSurface)
               || HasTrueColor(theme.Primary)
               || HasTrueColor(theme.OnPrimary)
               || HasTrueColor(theme.Secondary)
               || HasTrueColor(theme.Accent)
               || HasTrueColor(theme.Success)
               || HasTrueColor(theme.Info)
               || HasTrueColor(theme.Warning)
               || HasTrueColor(theme.Danger);
    }

    private static bool HasTrueColor(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.TrimStart().StartsWith('#');

    public static Scheme BuildScheme(StraumrTheme theme)
    {
        Color surface = Resolve(theme.Surface);
        Color onSurface = Resolve(theme.OnSurface);
        Color accent = Resolve(theme.Accent);
        Color secondary = Resolve(theme.Secondary);

        return new Scheme(new TuiAttribute(onSurface, surface))
        {
            Focus = new TuiAttribute(onSurface, surface),
            HotNormal = new TuiAttribute(accent, surface),
            HotFocus = new TuiAttribute(accent, surface),
            Disabled = new TuiAttribute(secondary, surface),
        };
    }

    public static Scheme BuildListScheme(StraumrTheme theme)
    {
        Color surface = Resolve(theme.Surface);
        Color onSurface = Resolve(theme.OnSurface);
        Color primary = Resolve(theme.Primary);
        Color onPrimary = Resolve(theme.OnPrimary);
        Color surfaceVariant = Resolve(theme.SurfaceVariant);
        Color accent = Resolve(theme.Accent);
        Color secondary = Resolve(theme.Secondary);

        return new Scheme(new TuiAttribute(onSurface, surface))
        {
            Focus = new TuiAttribute(primary, surfaceVariant),
            HotNormal = new TuiAttribute(accent, surface),
            HotFocus = new TuiAttribute(onPrimary, primary),
            Disabled = new TuiAttribute(secondary, surface),
        };
    }

    public static Scheme BuildButtonScheme(StraumrTheme theme)
    {
        Color surface = Resolve(theme.Surface);
        Color onSurface = Resolve(theme.OnSurface);
        Color primary = Resolve(theme.Primary);
        Color onPrimary = Resolve(theme.OnPrimary);
        Color secondary = Resolve(theme.Secondary);

        return new Scheme(new TuiAttribute(onSurface, surface))
        {
            Focus = new TuiAttribute(onPrimary, primary),
            HotNormal = new TuiAttribute(primary, surface),
            HotFocus = new TuiAttribute(onPrimary, primary),
            Disabled = new TuiAttribute(secondary, surface),
        };
    }
}
