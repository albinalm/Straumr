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

        if (key == Key.CursorUp || key == Key.CursorDown)
        {
            int logicalCount = (Source?.Count ?? 0) / MarkupLabelListDataSource.RowsPerItem;
            if (logicalCount == 0)
            {
                return true;
            }

            int currentLogical = (SelectedItem ?? 0) / MarkupLabelListDataSource.RowsPerItem;
            int nextLogical = key == Key.CursorUp
                ? Math.Max(0, currentLogical - 1)
                : Math.Min(logicalCount - 1, currentLogical + 1);
            SelectedItem = nextLogical * MarkupLabelListDataSource.RowsPerItem;
            return true;
        }

        if (key == Key.Home)
        {
            SelectedItem = 0;
            return true;
        }

        if (key == Key.End)
        {
            int logicalCount = (Source?.Count ?? 0) / MarkupLabelListDataSource.RowsPerItem;
            if (logicalCount > 0)
            {
                SelectedItem = (logicalCount - 1) * MarkupLabelListDataSource.RowsPerItem;
            }

            return true;
        }

        if (key == Key.PageUp || key == Key.PageDown || key == Key.Enter)
        {
            return base.OnKeyDown(key);
        }

        return true;
    }
}
