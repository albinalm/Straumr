using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// One movable divider between two regions, expressed as the first region's share of the pair.
/// </summary>
/// <remarks>
/// The share drives a bound <see cref="GridLength.Star"/> weight rather than a cell count, so a
/// divider keeps its proportion when the terminal is resized. Definitions are bound rather than
/// assigned because a screen that rebuilds its detail sections hands the grid new definitions each
/// time, while the split itself has to outlive them and stay the same object the keys move.
/// </remarks>
internal sealed class PaneSplit
{
    private const int Minimum = 15;
    private const int Maximum = 85;
    private const int Step = 4;

    private readonly int _initial;
    private readonly State<GridLength> _first;
    private readonly State<GridLength> _second;
    private int _share;

    public PaneSplit(int share)
    {
        _initial = share;
        _share = share;
        _first = new State<GridLength>(GridLength.Star(share));
        _second = new State<GridLength>(GridLength.Star(100 - share));
    }

    public ColumnDefinition FirstColumn() => Column(_first);

    public ColumnDefinition SecondColumn() => Column(_second);

    public RowDefinition FirstRow() => Row(_first);

    public RowDefinition SecondRow() => Row(_second);

    /// <summary>Moves the divider one step per <paramref name="steps"/>, away from the first region.</summary>
    public void Move(int steps) => SetShare(_share + steps * Step);

    public void Reset() => SetShare(_initial);

    private void SetShare(int share)
    {
        share = Math.Clamp(share, Minimum, Maximum);
        if (share == _share)
            return;

        _share = share;
        _first.Value = GridLength.Star(share);
        _second.Value = GridLength.Star(100 - share);
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

/// <summary>
/// The dividers one resource screen can move. A screen owns one instance for the life of its retained
/// tree, so a divider a user has moved survives selection changes, filtering and navigation.
/// </summary>
internal sealed class PaneSplits
{
    /// <summary>The list panel against the detail panel.</summary>
    public PaneSplit Panels { get; } = new(31);

    /// <summary>The two titled sections of the detail panel.</summary>
    public PaneSplit Sections { get; } = new(48);

    /// <summary>The detail sections against a pane stacked under them.</summary>
    public PaneSplit Stack { get; } = new(50);

    /// <summary>
    /// Whether this screen stacks a pane under its sections. Only such a screen has a horizontal
    /// divider to move, so on one without it the vertical keys stay inert rather than silent.
    /// </summary>
    public bool HasStack { get; private set; }

    internal void UseStack() => HasStack = true;
}
