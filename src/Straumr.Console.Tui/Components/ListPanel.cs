using System.Collections.ObjectModel;
using Straumr.Console.Tui.Components.Base;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components;

public class ListPanel : TuiComponent
{
    public required string Title { get; init; }
    public Pos X { get; init; } = 0;
    public Pos Y { get; init; } = 0;
    public Dim Width { get; init; } = Dim.Fill();
    public Dim Height { get; init; } = Dim.Fill();
    public required IReadOnlyList<string> Items { get; init; }

    public event Action<int>? ItemActivated;

    public override View Build()
    {
        var items = new ObservableCollection<string>(Items);

        ListView list = new()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Source = new ListWrapper<string>(items),
        };

        list.Accepting += (_, _) =>
        {
            if (list.SelectedItem is >= 0)
                ItemActivated?.Invoke(list.SelectedItem.Value);
        };

        FrameView frame = new()
        {
            Title = Title,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
        };

        frame.Add(list);
        return frame;
    }
}
