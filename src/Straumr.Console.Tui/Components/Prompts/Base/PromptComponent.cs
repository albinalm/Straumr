using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Base;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.Base;

internal abstract class PromptComponent : TuiComponent
{
    public StraumrTheme? Theme { get; init; }

    protected Scheme? BuildListScheme()
    {
        return Theme is not null ? ColorResolver.BuildListScheme(Theme) : null;
    }

    protected Scheme? BuildButtonScheme()
    {
        return Theme is not null ? ColorResolver.BuildButtonScheme(Theme) : null;
    }

    protected static FrameView CreateFrame(string title)
    {
        return new FrameView
        {
            Title = title,
            X = 2,
            Y = Banner.FigletHeight + 2,
            Width = Dim.Fill(4),
            Height = Dim.Fill(2),
        };
    }
}
