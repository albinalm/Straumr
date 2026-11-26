using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

public sealed class ThemeSelectionService(IStraumrSettingsService settings)
{
    private StraumrChromeModel? _appliedChrome;
    private StraumrPaletteModel? _applied;

    public string? Message { get; private set; }

    internal bool Preview(StraumrThemeModel theme) => Use(WithOverrides(theme, out _));

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

        StraumrThemeModel effective = WithOverrides(theme, out string? problem);
        Message ??= problem;
        return Use(effective);
    }

    private bool Use(StraumrThemeModel theme)
    {
        bool changed = theme.Palette != _applied || theme.Chrome != _appliedChrome;
        _applied = theme.Palette;
        _appliedChrome = theme.Chrome;
        StraumrStyleService.Apply(theme);
        return changed;
    }

    private StraumrThemeModel WithOverrides(StraumrThemeModel theme, out string? problem)
    {
        problem = null;
        StraumrChromeModel chrome = theme.Chrome;
        if (settings.Settings.Layout is { } written)
        {
            if (StraumrThemeService.TryParseLayout(written, out StraumrLayoutKind layout))
            {
                chrome = chrome with { Layout = layout };
            }
            else
            {
                problem = $"settings.toml: layout is \"{written.Trim()}\"; use v1 or v2";
            }
        }

        if (settings.Settings.Animations is { } animations)
        {
            chrome = chrome with { Motion = animations ? StraumrMotionModel.All : StraumrMotionModel.Still };
        }

        return ReferenceEquals(chrome, theme.Chrome) ? theme : theme with { Chrome = chrome };
    }
}
