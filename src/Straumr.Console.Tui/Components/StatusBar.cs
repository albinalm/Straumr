using Straumr.Console.Tui.Components.Base;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components;

internal class StatusBar : TuiComponent
{
    private Label? _label;
    private CancellationTokenSource? _hideCts;

    public override View Build()
    {
        _label = new Label
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            Height = 1,
            TextAlignment = Alignment.Center,
            Visible = false
        };

        return _label;
    }

    public void ShowSuccess(string message)
    {
        if (_label is null)
            return;

        _hideCts?.Cancel();
        _hideCts = new CancellationTokenSource();
        var token = _hideCts.Token;

        _label.Text = message;
        _label.Visible = true;
        var green = new TuiAttribute(Color.BrightGreen, Color.Black);
       _label.SetScheme(new Scheme(green) { Focus = green });

        _ = HideAfterDelayAsync(token);
    }

    private async Task HideAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(2000, token);
            if (_label is not null)
            {
                _label.Visible = false;
            }
        }
        catch (TaskCanceledException) { }
    }
}
