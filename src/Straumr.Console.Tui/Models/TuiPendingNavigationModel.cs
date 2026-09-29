using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Models;

internal readonly record struct TuiPendingNavigationModel(
    TuiScreen Screen,
    string Command,
    bool RunInPlace,
    TuiScreen? ReturnScreen)
{
    public ReferenceModel? Reference { get; init; }
    public Visual? FocusTarget { get; init; }
    public char? OpeningEcho { get; init; }
}
