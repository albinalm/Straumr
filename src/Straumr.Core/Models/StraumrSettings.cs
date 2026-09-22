using Tomlyn.Serialization;

namespace Straumr.Core.Models;

public sealed class StraumrSettings
{

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

        # Named defaults, followed by your individual [keybinds] overrides.
        # Choices: vim (default), emacs, commander, client.
        # keybind-preset = "vim"

        # Absent or false shows the quick start. Run it again with :quickstart.
        # quick-start-completed = false

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

        # Whether a JSON body is coloured by token: keys, strings, numbers, the two literals and
        # the punctuation between them. The colours are the theme's `[code]` table. Default: true.
        #
        #   highlight = false
        #
        # `h` on the response body turns it on and off by hand whatever this says.

        # The size, in KiB, past which a body arrives uncoloured. Colouring walks every line that
        # is drawn, which a body of several megabytes is felt through; past this it is off until
        # `h` asks for it. Default: 256.
        #
        #   highlight-limit = 1024

        # The largest response body, in KiB, that a request file keeps. A sent response is written
        # into the request beside the request itself, so the pane still shows it after a `:refresh`
        # or a restart. One larger than this is kept without its body — the status, the headers and
        # the measurements stay, and the Body page says the body was not saved. 0 keeps nothing.
        # Default: 1024.
        #
        #   store-limit = 4096

        [keybinds]
        # Override only the actions you want to change. Everything else keeps its default.
        # Quote action names (the dots are part of the name). Every action, and every preset, is listed at
        # https://github.com/albinalm/Straumr/blob/main/docs/keybinds.md
        #
        #   "Request.Send" = "Ctrl+R"
        #   "Editor.Save" = "F2"
        #   "ResourceList.Next" = "Down"
        # Use "none" to disable a binding, or remove the entry to restore its default.

        """;
    [TomlPropertyName("theme")]
    public string? Theme { get; set; }

    [TomlPropertyName("keybind-preset")]
    public string? KeybindPreset { get; set; }

    [TomlPropertyName("quick-start-completed")]
    public bool QuickStartCompleted { get; set; }

    [TomlPropertyName("paths")]
    public StraumrPathSettings Paths { get; set; } = new();

    [TomlPropertyName("response")]
    public StraumrResponseSettings Response { get; set; } = new();

    [TomlPropertyName("keybinds")]
    public Dictionary<string, string> Keybinds { get; set; } = new(StringComparer.Ordinal);
}
