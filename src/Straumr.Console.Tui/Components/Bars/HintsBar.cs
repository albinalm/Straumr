using Straumr.Console.Tui.Components.Base;
using Straumr.Console.Tui.Components.Branding;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Bars;

internal class HintsBar : TuiComponent
{
    private TextView? _textView;

    public required string Text { get; init; }

    public override View Build()
    {
        View container = new View
        {
            X = 3,
            Width = Dim.Fill(Banner.FigletWidth + 6),
            Height = Banner.FigletHeight + 1,
        };

        _textView = new TextView
        {
            Text = Text,
            ReadOnly = true,
            WordWrap = true,
            Enabled = false,
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Auto(),
        };

        container.Add(_textView);
        return container;
    }

    public void UpdateText(string text)
    {
        _textView?.Text = text;
    }
}
