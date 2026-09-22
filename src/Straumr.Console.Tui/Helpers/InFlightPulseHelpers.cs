using System.Diagnostics;
using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Helpers;

internal static class InFlightPulseHelpers
{
    public static Visual Create(string label, Stopwatch clock) =>
        new HStack(
                new Spinner().Style(StraumrStyleService.RequestPulse),
                new InFlightElapsedLabel(label, clock))
            .Spacing(2);
}
