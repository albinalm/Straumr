using Tomlyn.Serialization;

namespace Straumr.Core.Models;

/// <summary>
/// The settings a reader writes by hand, in <c>~/.straumr/settings.toml</c>.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="StraumrState"/>. Options are the app's own state — the
/// workspace registry, which workspace is current, where panes were dragged to — written by the
/// program and never meant to be typed. Settings are the reader's, written in their editor and only
/// read here. Keeping them in one file would mean rewriting a commented dotfile every time a pane
/// moved, which no format survives.
/// </remarks>
public sealed class StraumrSettings
{
    /// <summary>
    /// A built-in or custom theme name, or an explicit path to a theme file.
    /// <see langword="null"/> means the default, which follows the terminal's own colours.
    /// </summary>
    [TomlPropertyName("theme")]
    public string? Theme { get; set; }

    /// <summary>Where the app puts what it creates when nothing says otherwise.</summary>
    [TomlPropertyName("paths")]
    public StraumrPathSettings Paths { get; set; } = new();

    [TomlPropertyName("response")]
    public StraumrResponseSettings Response { get; set; } = new();

    /// <summary>
    /// What the file says when there is none. Written on first read so the reader has something to
    /// open and something to copy, rather than an empty buffer they have to know the keys for.
    /// </summary>
    public const string Template =
        """
        # Straumr settings. Reopen this file any time with `:settings`.
        # Values not written here take the defaults shown in the comments.

        # The theme. Either a built-in or custom name, or a path to a theme file.
        #
        #   "terminal"   follow the terminal's own colours and palette (default)
        #   "straumr"   the dark blue Straumr palette
        #
        # A custom theme name is looked up in ~/.straumr/themes; `.toml` may be omitted:
        #
        #   theme = "mine"
        #
        # A theme file carries only what it wants to be different; everything it leaves out follows
        # the terminal. A whole theme can be three lines:
        #
        #   name = "Mine"
        #   [colors]
        #   brand = "magenta"
        #
        # To start from the dark palette instead, `:theme export straumr` writes it out as a file
        # to copy and change.
        theme = "terminal"

        [paths]
        # Where a new workspace is offered. Unset, the app suggests nowhere and you pick each time.
        # A leading ~ and environment variables are expanded.
        #
        #   workspaces = "~/code/apis"

        # Where the global secret store lives. Defaults to ~/.straumr/secrets.
        #
        #   secrets = "~/.straumr/secrets"

        [response]
        # What to do with a JSON response body the moment it arrives.
        #
        #   "none"       show it exactly as it was sent (default)
        #   "beautify"   lay it out over indented lines
        #   "minify"     strip it down to one line
        #
        # A body that is not JSON is shown as it was sent whatever this says, and `b` on the
        # response pane still beautifies and minifies by hand either way.
        format = "none"

        """;
}

public sealed class StraumrResponseSettings
{
    [TomlPropertyName("format")]
    public string? Format { get; set; }
}

/// <summary>
/// The directories the app writes into when the reader has not named one. They live here rather
/// than beside the workspace registry because they are a preference typed by hand, not a fact the
/// program discovered — and because a file the program rewrites on every pane drag is no place to
/// keep something with a comment above it.
/// </summary>
public sealed class StraumrPathSettings
{
    /// <summary>Where a new workspace is offered. Unset means the app does not suggest one.</summary>
    [TomlPropertyName("workspaces")]
    public string? Workspaces { get; set; }

    /// <summary>Where the global secret store lives. Unset means <c>~/.straumr/secrets</c>.</summary>
    [TomlPropertyName("secrets")]
    public string? Secrets { get; set; }
}
