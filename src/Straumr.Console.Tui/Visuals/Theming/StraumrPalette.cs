using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Visuals.Theming;

/// <summary>
/// The colours a theme names, one per role the shell paints with. Every style in
/// <see cref="Shared.StraumrStyles"/> is built from these and nothing else, so a theme is this
/// record and a theme file is its eighteen keys.
/// </summary>
/// <remarks>
/// The roles are semantic rather than literal: <see cref="Amber"/> is "a populated count", not a
/// request for orange, and a theme is free to answer it with whatever its scheme uses for that
/// meaning. The set is deliberately closed. A new role means every theme file in existence is
/// missing a key, so add one only when the design contract gains a distinction that cannot be
/// expressed with these.
/// </remarks>
public sealed record StraumrPalette
{
    /// <summary>The one ground the whole app shares. Regions are told apart by dividers alone.</summary>
    public required Color Background { get; init; }

    /// <summary>The only surface raised above the background, used by an identifier badge.</summary>
    public required Color Raised { get; init; }

    /// <summary>The band under a selected row in a focused list, and the fill of the focus chip.</summary>
    /// <remarks>
    /// Ignored as a band when <see cref="SelectionInverts"/> is set, where it survives only as the
    /// highlight behind selected text inside a field — the one selection the terminal draws for us
    /// nowhere and which therefore still needs a colour of its own.
    /// </remarks>
    public required Color Selection { get; init; }

    /// <summary>
    /// Whether the focused selection is painted by inverting the terminal's own two colours rather
    /// than by filling with <see cref="Selection"/>.
    /// </summary>
    /// <remarks>
    /// This is the only way to mark a row in colours the app cannot see. No terminal reports its
    /// scheme to a program portably, so a theme that wants the reader's own colours cannot name
    /// them — but it can ask for them to be swapped, which is what every terminal already does for
    /// its own selection and what `vim` and `less` do for a current line. It costs the second level
    /// of emphasis: everything on the band must leave its foreground at the terminal's default, or
    /// the band inverts into two tones rather than one.
    /// </remarks>
    public bool SelectionInverts { get; init; }

    /// <summary>The band under a selected row whose list does not own focus.</summary>
    public required Color SelectionInactive { get; init; }

    /// <summary>The faintest of the three row levels, under the pointer.</summary>
    public required Color Hover { get; init; }

    public required Color Border { get; init; }

    /// <summary>Scroll bar chrome, kept below the dividers so it never competes with content.</summary>
    public required Color ScrollTrack { get; init; }

    public required Color ScrollThumb { get; init; }

    public required Color Text { get; init; }

    /// <summary>Ordinary text lifted onto the selection band, where <see cref="Text"/> would fall short.</summary>
    public required Color TextBright { get; init; }

    public required Color Muted { get; init; }

    public required Color MutedBright { get; init; }

    public required Color Accent { get; init; }

    /// <summary>A populated count or a value that is present; its absence reads inert.</summary>
    public required Color Amber { get; init; }

    /// <summary>Reserved for the active workspace and the marker naming the current resource.</summary>
    public required Color Green { get; init; }

    public required Color Red { get; init; }

    /// <summary>Red lifted onto the selection band, as <see cref="TextBright"/> lifts ordinary text.</summary>
    public required Color RedBright { get; init; }

    public required Color Purple { get; init; }

    /// <summary>
    /// The identity mark, <c>{straumr}</c>. Optional in a theme file, where it defaults to
    /// <see cref="Accent"/>.
    /// </summary>
    /// <remarks>
    /// Its own role because a brand is not an accent. They were one colour until a theme wanted
    /// colourless chrome and made the wordmark vanish with the keys, which is the sort of thing one
    /// role doing two jobs does.
    /// </remarks>
    public required Color Brand { get; init; }

    /// <summary>What each HTTP method reads as. The most scanned colour in the app.</summary>
    public required MethodPalette Methods { get; init; }

    public required CodePalette Code { get; init; }
}

/// <summary>
/// A colour per HTTP method. Semantic through and through: the method is the first thing a reader
/// looks for in a list of requests, and telling them apart at a glance is the job.
/// </summary>
/// <remarks>
/// Its own type rather than free-standing roles so a theme can carry the set as one <c>[methods]</c>
/// table, and a record of colours rather than a dictionary so two palettes still compare by value —
/// which is what decides whether a theme change is worth rebuilding the shell for.
/// </remarks>
public sealed record MethodPalette(
    Color Get,
    Color Post,
    Color Put,
    Color Patch,
    Color Delete,
    Color Other);

public sealed record CodePalette(
    Color Key,
    Color String,
    Color Number,
    Color Boolean,
    Color Null,
    Color Punctuation);
