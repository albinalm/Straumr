namespace Straumr.Console.Tui.Visuals.Theming;

/// <summary>
/// The themes that ship in the binary, written in the same format a reader's own theme is.
/// </summary>
/// <remarks>
/// They are TOML text rather than constructed palettes so there is one definition of each theme
/// rather than two, exporting one is writing the text out, and the files people start from have
/// been through the same parser their edits will be. A malformed built-in is a build-time mistake
/// and <see cref="StraumrThemes"/> throws on it rather than starting with a half-applied palette.
/// </remarks>
internal static class BuiltInThemes
{
    public const string TerminalName = "terminal";
    public const string StraumrName = "straumr";

    /// <summary>
    /// The default. Every role is either the terminal's own default or one of its sixteen palette
    /// slots, so the app inherits whatever scheme the reader has configured instead of painting
    /// over it.
    /// </summary>
    public const string Terminal =
        """
        # Straumr's default theme: follow the terminal's own colours.
        #
        # Also the theme every other one is layered over. A theme file writes only the roles it
        # wants to change and inherits the rest from here, which is why this file is the one that
        # has to name them all.
        #
        # Values here are palette slots rather than hex, so they change with the reader's scheme.
        #   default        the terminal's own foreground or background, painted as nothing at all
        #   blue, red, …   black red green yellow blue magenta cyan white, each with a bright- form
        #   indexed:N      one of the 256 indexed colours, for a scheme that goes beyond sixteen
        #
        # The chrome here is deliberately colourless. A scheme this theme cannot see may be built
        # around any hue, so every hue it introduces of its own is a guess at somebody's taste —
        # and on a red or a warm scheme a blue accent reads as a foreign object. The only hues left
        # are the ones that carry meaning and would be lost without them: the method colours, a
        # populated count, the active workspace, an error. Everything structural — keys, markers,
        # rules, badges — is the terminal's own foreground, dimmed or brightened.
        name = "Terminal"

        [colors]
        # The terminal's own ground and text. `default` paints no colour at all, which is what
        # keeps a transparent terminal transparent rather than covering it with a dark rectangle.
        background         = "default"
        text               = "default"

        # There is no band fainter than the background when the background is unknown: the sixteen
        # colours hold no tint of it, and a wrong guess reads as a stripe rather than a highlight.
        # Hover and the unfocused selection therefore paint nothing, which leaves the selected row
        # of an unfocused list marked by its bar alone. A theme that names its own background can
        # have all three levels; see straumr.
        hover              = "default"
        selection-inactive = "default"

        # The selected row of a focused list. `invert` is not a colour: it swaps the terminal's own
        # foreground and background for that row, which is how the band ends up in the reader's
        # colours rather than in one this file guessed at. It is what the terminal already does for
        # its own selection, and what vim and less do for a current line.
        #
        # Inversion swaps whatever colours the cell has, so everything that lands on the band leaves
        # its foreground at `default` below — give one of them a colour and the band comes out in
        # two tones, the name line one shade and the meta line under it another.
        #
        # Name a colour here instead (`blue`, `magenta`, `bright-black`, `#4a5568`) and the band is
        # filled with it in the ordinary way; `text-bright` and `muted-bright` then want colours
        # that read on it.
        selection          = "invert"

        # Both are real colours rather than `default`, because they are not only the text that lands
        # on the selection band — they are every button label, the dropdown, the text a field is
        # typed into and the badge. Leaving them at the terminal's own foreground gave all of those
        # no colour at all, which reads as bold nothing. The cost is that a selected row is very
        # slightly two-toned, since inversion swaps whatever colour a cell carries: on a dark scheme
        # white and the terminal's own foreground are near enough that it does not show.
        text-bright        = "bright-white"
        muted              = "bright-black"
        muted-bright       = "white"

        raised             = "bright-black"
        border             = "bright-black"
        scroll-track       = "bright-black"
        scroll-thumb       = "white"

        # Keys, the focus chip's text and the marker on a selected row. Colourless on purpose: it
        # has to read on the terminal's ground and on the selection band, and being brighter than
        # the muted text around it is the whole of the job.
        accent             = "bright-white"

        # The hues that are left are the ones that mean something. These are palette slots, so they
        # are the reader's own green and red rather than colours chosen here — which is why colour
        # that carries meaning belongs in this theme and colour that decorates does not.
        amber              = "yellow"
        green              = "green"
        red                = "red"
        red-bright         = "bright-red"
        purple             = "magenta"

        # The identity mark, `{straumr}`. The one place this theme spends a hue on something that is
        # not information — it is a name, and a product that renders its own name in body text has
        # given something up. One word, and one line to change.
        brand              = "cyan"

        # The method is the first thing a reader looks for in a list of requests, so telling the
        # methods apart is worth more than the restraint the rest of this file shows. The mapping is
        # the conventional one, in palette slots so every colour is the reader's own.
        [methods]
        get                = "green"
        post               = "blue"
        put                = "yellow"
        patch              = "magenta"
        delete             = "red"
        other              = "default"

        # A coloured response body, on the same terms as the methods: what a token is coloured by
        # is what kind of value it is, which is information rather than decoration, and the slots
        # are the reader's own hues rather than ones chosen here. The mapping is the conventional
        # one, so a body reads the way it reads in the editor beside this terminal.
        [code]
        key                = "cyan"
        string             = "green"
        number             = "yellow"
        boolean            = "magenta"
        null               = "bright-black"
        punctuation        = "bright-black"
        """;

    /// <summary>
    /// The palette the shell was designed against, kept under a name now that it is no longer the
    /// only one. Every pairing stays at or above 4.5:1 for text and 3:1 for glyphs.
    /// </summary>
    public const string Straumr =
        """
        # The dark blue Straumr palette. Fixed colours: this theme ignores the terminal's scheme.
        #
        # It names every role, which a theme need not do — anything left out is inherited from the
        # terminal theme. A palette that fixes its own background has to name them all, because
        # inheriting a colour meant for an unknown ground would land it on this one.
        #
        # The surfaces sit near hue 222 and carry higher chroma on the foregrounds, so the screen
        # reads as live rather than flat. The whole app shares one background; regions are told
        # apart by dividers alone, and the dark ground is what makes the accents carry.
        name = "Straumr"

        [colors]
        background         = "#090D15"

        # The only surface raised above the background, used by an identifier badge.
        raised             = "#1B2438"

        selection          = "#21418F"
        selection-inactive = "#2C3D68"
        hover              = "#151E33"
        border             = "#364670"

        # Scroll bar chrome, kept below the dividers so it never competes with content.
        scroll-track       = "#1E2740"
        scroll-thumb       = "#43567E"

        text               = "#D9E0F0"
        text-bright        = "#EFF4FF"
        muted              = "#8FA0C4"
        muted-bright       = "#B9C6E0"

        accent             = "#3B9EFF"
        amber              = "#FFC857"
        green              = "#3DDC97"
        red                = "#FF6B7A"

        # Red for text on the selection band, where the base red falls under 4.5:1.
        red-bright         = "#FF9BA6"
        purple             = "#B79CFF"

        # A fixed palette names its own brand and methods rather than inheriting the terminal's,
        # which is what an unwritten role falls back to.
        brand              = "#3B9EFF"

        [methods]
        get                = "#3DDC97"
        post               = "#3B9EFF"
        put                = "#FFC857"
        patch              = "#B79CFF"
        delete             = "#FF6B7A"
        other              = "#B9C6E0"

        # A coloured response body. Drawn from the palette above rather than from hues of its own:
        # a body is read against the same ground as everything else, and six more colours would be
        # six more pairings to keep at contrast.
        [code]
        key                = "#3B9EFF"
        string             = "#3DDC97"
        number             = "#FFC857"
        boolean            = "#B79CFF"
        null               = "#8FA0C4"
        punctuation        = "#8FA0C4"
        """;
}
