using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Models;

public sealed record StraumrChromeModel
{
    public required StraumrLayoutKind Layout { get; init; }

    public required StraumrMotionModel Motion { get; init; }

    public required IReadOnlyList<Color> Ramp { get; init; }

    public bool Equals(StraumrChromeModel? other) =>
        other is not null && Layout == other.Layout && Motion == other.Motion &&
        Ramp.SequenceEqual(other.Ramp);

    public override int GetHashCode() => HashCode.Combine(Layout, Motion, Ramp.Count);
}
