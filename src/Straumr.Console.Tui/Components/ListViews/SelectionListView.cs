using System.Collections.ObjectModel;
using Straumr.Console.Tui.Components.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.ListViews;

internal sealed class SelectionListView : ListView
{
    private readonly Func<Key, bool> _onKey;

    public SelectionListView(Func<Key, bool> onKey, Scheme? scheme = null)
    {
        _onKey = onKey;
        if (scheme is not null)
        {
            SetScheme(scheme);
        }
    }

    public void SetMarkupSource(ObservableCollection<MarkupLabel> source)
    {
        Source = new MarkupLabelListDataSource(source);
    }

    protected override bool OnKeyDown(Key key)
    {
        if (_onKey(key))
        {
            return true;
        }

        // Only allow arrow keys and Enter through to the base ListView.
        // Block everything else (letters, etc.) to prevent type-ahead search.
        if (key == Key.CursorUp || key == Key.CursorDown
            || key == Key.PageUp || key == Key.PageDown
            || key == Key.Home || key == Key.End
            || key == Key.Enter)
        {
            return base.OnKeyDown(key);
        }

        return true;
    }
}
