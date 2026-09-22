using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Models;

public sealed record StraumrPaletteModel
{
    public required Color Background { get; init; }

    public required Color Raised { get; init; }

    public required Color Selection { get; init; }

    public bool SelectionInverts { get; init; }

    public required Color SelectionInactive { get; init; }

    public required Color Hover { get; init; }

    public required Color Border { get; init; }

    public required Color ScrollTrack { get; init; }

    public required Color ScrollThumb { get; init; }

    public required Color Text { get; init; }

    public required Color TextBright { get; init; }

    public required Color Muted { get; init; }

    public required Color MutedBright { get; init; }

    public required Color Accent { get; init; }

    public required Color Amber { get; init; }

    public required Color Green { get; init; }

    public required Color Red { get; init; }

    public required Color RedBright { get; init; }

    public required Color Purple { get; init; }

    public required Color Brand { get; init; }

    public required MethodPaletteModel Methods { get; init; }

    public required CodePaletteModel Code { get; init; }
}
