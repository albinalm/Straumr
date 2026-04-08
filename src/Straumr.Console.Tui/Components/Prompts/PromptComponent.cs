using Straumr.Console.Tui.Components.Base;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts;

internal abstract class PromptComponent : TuiComponent
{
    protected static FrameView CreateFrame(string title)
    {
        return new FrameView
        {
            Title = title,
            X = 2,
            Y = 1,
            Width = Dim.Fill(4),
            Height = Dim.Fill(2),
        };
    }
}
