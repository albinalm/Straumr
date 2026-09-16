using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Console.Tui.Visuals.Theming;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Infrastructure;

/// <summary>
/// Applies the theme the settings file names, and says whether doing so changed anything.
/// </summary>
/// <remarks>
/// It outlives the shell it themes. The visual tree is rebuilt when the palette changes — styles
/// are values a control is handed once, not bindings it re-reads — so the object that remembers
/// which palette is already on screen has to be the one thing that survives that rebuild.
/// </remarks>
public sealed class ThemeSelection(IStraumrSettingsService settings)
{
    private StraumrPalette? _applied;

    /// <summary>
    /// What went wrong, or what is worth knowing, about the theme last applied: unparsable settings,
    /// a theme that could not be resolved, or a palette whose roles collide. <see langword="null"/>
    /// when there is nothing to say.
    /// </summary>
    public string? Message { get; private set; }

    /// <summary>
    /// Resolves and applies the theme. Returns whether the palette actually changed, which is the
    /// only thing that makes rebuilding the shell worth doing — reopening the settings file and
    /// saving it unchanged should not throw the reader's screen away.
    /// </summary>
    /// <remarks>
    /// The comparison is of the palette rather than of the name that produced it, so editing a theme
    /// file in place is picked up even though the settings still point at the same path.
    /// </remarks>
    public bool Apply()
    {
        Message = settings.Problem;
        string reference = settings.Settings.Theme?.Trim() is { Length: > 0 } named
            ? named
            : StraumrThemes.DefaultReference;

        if (!StraumrThemes.TryResolve(reference, settings.SettingsDirectory, out StraumrTheme theme, out string? error))
        {
            // A theme that cannot be resolved falls back to the default rather than refusing to
            // start. The message is what tells the reader their setting did not take.
            Message = error;
            StraumrThemes.TryResolve(
                StraumrThemes.DefaultReference, settings.SettingsDirectory, out theme, out _);
        }
        else if (Message is null && theme.Warnings.Count > 0)
        {
            Message = $"{theme.Name}: {theme.Warnings[0]}";
        }

        if (theme.Palette == _applied)
            return false;

        _applied = theme.Palette;
        StraumrStyles.Apply(theme);
        return true;
    }
}
