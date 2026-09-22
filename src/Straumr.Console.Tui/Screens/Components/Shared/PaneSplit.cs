using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class PaneSplit
{
    private const int Minimum = 15;
    private const int Maximum = 85;
    private const int Step = 4;
    private readonly State<GridLength> _first;

    private readonly int _initial;
    private readonly State<GridLength> _second;

    public PaneSplit(int share)
    {
        share = Math.Clamp(share, Minimum, Maximum);
        _initial = share;
        Share = share;
        _first = new State<GridLength>(GridLength.Star(share));
        _second = new State<GridLength>(GridLength.Star(100 - share));
    }

    public int Share { get; private set; }

    public event Action? Changed;

    public ColumnDefinition FirstColumn() => Column(_first);

    public ColumnDefinition SecondColumn() => Column(_second);

    public RowDefinition FirstRow() => Row(_first);

    public RowDefinition SecondRow() => Row(_second);

    public void Move(int steps) => SetShare(Share + steps * Step);

    public void Reset() => SetShare(_initial);

    private void SetShare(int share)
    {
        share = Math.Clamp(share, Minimum, Maximum);
        if (share == Share)
        {
            return;
        }

        Share = share;
        _first.Value = GridLength.Star(share);
        _second.Value = GridLength.Star(100 - share);
        Changed?.Invoke();
    }

    private static ColumnDefinition Column(State<GridLength> width)
    {
        var column = new ColumnDefinition();
        column.BindWidth(width);
        return column;
    }

    private static RowDefinition Row(State<GridLength> height)
    {
        var row = new RowDefinition();
        row.BindHeight(height);
        return row;
    }
}
