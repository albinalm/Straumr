using Straumr.Console.Tui.Components.Base;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components;

internal class HintsBar : TuiComponent
{
    private TextView? _textView;

    public required string Text { get; init; }

    public override View Build()
    {
        var container = new View
        {
            X = 3,
            Y = 0,
            Width = Dim.Fill(Branding.FigletWidth + 6),
            Height = Branding.FigletHeight + 1,
        };

        _textView = new TextView
        {
            Text = Text,
            ReadOnly = true,
            WordWrap = true,
            Enabled = false,
            X = 0,
            Y = Pos.Center(),
            Width = Dim.Fill(),
            Height = Dim.Auto(),
        };

        container.Add(_textView);
        return container;
    }

    public void UpdateText(string text)
    {
        if (_textView is not null)
            _textView.Text = text;
    }
}
