using Straumr.Console.Tui.Components.Base;
using Straumr.Console.Tui.Theme;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts;

internal abstract class PromptComponent : TuiComponent
{
    public TuiTheme? Theme { get; init; }

    protected Scheme? BuildListScheme()
    {
        return Theme is not null ? TuiColors.BuildListScheme(Theme) : null;
    }

    protected static FrameView CreateFrame(string title)
    {
        return new FrameView
        {
            Title = title,
            X = 2,
            Y = Branding.FigletHeight + 2,
            Width = Dim.Fill(4),
            Height = Dim.Fill(2),
        };
    }
}
