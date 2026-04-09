using Straumr.Console.Tui.Components.Base;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.Bars;

internal class StatusNotificationBar : TuiComponent
{
    private Label? _label;
    private CancellationTokenSource? _hideCts;
    private string? _pendingMessage;

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

        TryShowPendingMessage();
        return _label;
    }

    public void ShowSuccess(string message)
    {
        _pendingMessage = message;
        TryShowPendingMessage();
    }

    private void TryShowPendingMessage()
    {
        if (_label is null || _pendingMessage is null)
        {
            return;
        }

        string message = _pendingMessage;
        _pendingMessage = null;
        ApplyMessage(message);
    }

    private void ApplyMessage(string message)
    {
        _hideCts?.Cancel();
        _hideCts = new CancellationTokenSource();
        CancellationToken token = _hideCts.Token;

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
            _label?.Visible = false;
        }
        catch (TaskCanceledException) { }
    }
}
