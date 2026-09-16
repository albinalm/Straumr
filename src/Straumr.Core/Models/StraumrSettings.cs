using Tomlyn.Serialization;

namespace Straumr.Core.Models;

/// <summary>
/// The settings a reader writes by hand, in <c>~/.straumr/settings.toml</c>.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="StraumrOptions"/>. Options are the app's own state — the
/// workspace registry, which workspace is current, where panes were dragged to — written by the
/// program and never meant to be typed. Settings are the reader's, written in their editor and only
/// read here. Keeping them in one file would mean rewriting a commented dotfile every time a pane
/// moved, which no format survives.
/// </remarks>
public sealed class StraumrSettings
{
    /// <summary>
    /// A built-in theme name, or a path to a theme file. <see langword="null"/> means the default,
    /// which follows the terminal's own colours.
    /// </summary>
    [TomlPropertyName("theme")]
    public string? Theme { get; set; }

    /// <summary>
    /// What the file says when there is none. Written on first read so the reader has something to
    /// open and something to copy, rather than an empty buffer they have to know the keys for.
    /// </summary>
    public const string Template =
        """
        # Straumr settings. Reopen this file any time with `:settings`.
        # Values not written here take the defaults shown in the comments.

        # The theme. Either a built-in name or a path to a theme file.
        #
        #   "terminal"   follow the terminal's own colours and palette (default)
        #   "deepocean"  the dark blue Straumr palette
        #
        # A path is taken relative to this file, so a theme kept beside it is just its name:
        #
        #   theme = "themes/mine.toml"
        #
        # `:theme export deepocean` writes a built-in out as a file to start from.
        theme = "terminal"

        """;
}
