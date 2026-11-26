namespace Straumr.Console.Tui.Models;

public sealed record StraumrMotionModel(bool Spinner, bool Pulse, bool Transitions)
{
    public static StraumrMotionModel All { get; } = new(true, true, true);

    public static StraumrMotionModel Still { get; } = new(false, false, false);

    public bool Any => Spinner || Pulse || Transitions;
}
