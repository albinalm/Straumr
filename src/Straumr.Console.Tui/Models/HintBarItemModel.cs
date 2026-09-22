using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Models;

internal sealed class HintBarItemModel
{
    public required Command Command { get; init; }

    public required Visual Target { get; init; }

    public required bool IsEnabled { get; init; }

    public required string KeyText { get; init; }

    public required string LabelText { get; init; }

    public required StyledRun[] LabelRuns { get; init; }

    public required int Width { get; init; }

    public int X { get; set; }

    public int Row { get; set; }

    public int SeparatorWidth { get; set; }
}
