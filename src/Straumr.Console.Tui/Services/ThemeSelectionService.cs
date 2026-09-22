using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

public sealed class ThemeSelectionService(IStraumrSettingsService settings)
{
    private StraumrPaletteModel? _applied;

    public string? Message { get; private set; }

    internal bool Preview(StraumrThemeModel theme)
    {
        bool changed = _applied != theme.Palette;
        _applied = theme.Palette;
        StraumrStyleService.Apply(theme);
        return changed;
    }

    public bool Apply()
    {
        Message = settings.Problem;
        string reference = settings.Settings.Theme?.Trim() is { Length: > 0 } named
            ? named
            : StraumrThemeService.DefaultReference;

        if (!StraumrThemeService.TryResolve(reference, settings.SettingsDirectory, out StraumrThemeModel theme, out string? error))
        {
            Message = error;
            StraumrThemeService.TryResolve(
                StraumrThemeService.DefaultReference, settings.SettingsDirectory, out theme, out _);
        }
        else if (Message is null && theme.Warnings.Count > 0)
        {
            Message = $"{theme.Name}: {theme.Warnings[0]}";
        }

        bool changed = theme.Palette != _applied;
        _applied = theme.Palette;
        StraumrStyleService.Apply(theme);
        return changed;
    }
}
