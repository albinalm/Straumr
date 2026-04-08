using Straumr.Console.Tui.Components.Base;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components;

public class Banner : TuiComponent
{
    public required string Text { get; init; }
    public Pos X { get; init; } = 0;
    public Pos Y { get; init; } = 0;

    public override View Build()
    {
        return new Label
        {
            Text = Text,
            X = X,
            Y = Y,
        };
    }
}
