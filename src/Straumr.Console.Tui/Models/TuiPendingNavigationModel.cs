namespace Straumr.Console.Tui.Models;

internal readonly record struct TuiPendingNavigationModel(
    TuiScreen Screen,
    string Command,
    bool RunInPlace,
    TuiScreen? ReturnScreen);
