using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Rendering;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class TerminalSurface : ContentVisual
{
    public TerminalSurface(Visual content)
    {
        Content = content;
        HorizontalAlignment = content.HorizontalAlignment;
        VerticalAlignment = content.VerticalAlignment;
    }
    protected override void RenderOverride(CellBuffer buffer) => StraumrStyleService.ClearTerminalSurface(buffer, Bounds);
}
