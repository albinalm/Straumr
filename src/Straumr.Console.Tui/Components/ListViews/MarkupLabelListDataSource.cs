using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Text;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace Straumr.Console.Tui.Components.ListViews;

internal sealed class MarkupLabelListDataSource : IListDataSource
{
    public const int RowsPerItem = 3;

    private readonly ObservableCollection<MarkupLabel> _items;
    private bool _disposed;

    public MarkupLabelListDataSource(ObservableCollection<MarkupLabel> items)
    {
        _items = items;
        _items.CollectionChanged += OnCollectionChanged;
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count => _items.Count * RowsPerItem;
    public int MaxItemLength => _items.Count == 0 ? 0 : _items.Max(GetItemLength);
    public bool SuspendCollectionChangedEvent { get; set; }

    public bool IsMarked(int item) => false;

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX)
    {
        Scheme? scheme = listView.GetScheme();
        TuiAttribute normalAttr = scheme?.Normal ?? listView.GetCurrentAttribute();

        int contentRow = row + listView.Viewport.Y;
        if (contentRow >= _items.Count * RowsPerItem)
        {
            for (int fx = 0; fx < width; fx++)
            {
                listView.SetAttribute(normalAttr);
                listView.AddRune(col + fx, row, new Rune(' '));
            }

            return;
        }

        int logicalIndex = item / RowsPerItem;
        int lineIndex = item % RowsPerItem;
        int selectedLogical = listView.SelectedItem.HasValue ? listView.SelectedItem.Value / RowsPerItem : -1;
        bool isSelected = logicalIndex == selectedLogical;

        string markup = logicalIndex >= 0 && logicalIndex < _items.Count ? _items[logicalIndex].Markup : string.Empty;
        StraumrTheme? theme = logicalIndex >= 0 && logicalIndex < _items.Count ? _items[logicalIndex].Theme : null;

        string[] lines = markup.Split('\n');
        string lineMarkup = lineIndex < lines.Length ? lines[lineIndex] : string.Empty;

        List<MarkupText.MarkupRun> runs = MarkupText.ParseRuns(lineMarkup, theme);

        TuiAttribute rowBase = isSelected
            ? (scheme?.Focus ?? listView.GetCurrentAttribute())
            : normalAttr;

        int x = 0;
        int currentColumn = 0;

        foreach (MarkupText.MarkupRun run in runs)
        {
            TuiAttribute attribute = isSelected
                ? new(run.Attribute.Foreground, rowBase.Background, run.Attribute.Style)
                : run.Attribute;

            foreach (char ch in run.Text)
            {
                if (currentColumn++ < viewportX)
                {
                    continue;
                }

                if (x >= width)
                {
                    break;
                }

                listView.SetAttribute(attribute);
                listView.AddRune(col + x, row, new Rune(ch));
                x++;
            }

            if (x >= width)
            {
                break;
            }
        }

        for (; x < width; x++)
        {
            listView.SetAttribute(rowBase);
            listView.AddRune(col + x, row, new Rune(' '));
        }

        listView.SetAttribute(normalAttr);
    }

    public bool RenderMark(ListView listView, int item, int row, bool isMarked, bool markMultiple) => false;

    public void SetMark(int item, bool value)
    {
    }

    public IList ToList() => _items;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _items.CollectionChanged -= OnCollectionChanged;
        _disposed = true;
    }

    private int GetItemLength(MarkupLabel label)
        => MarkupText.ToPlain(label.Markup)
            .Split('\n')
            .DefaultIfEmpty(string.Empty)
            .Max(line => line.Length);

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(sender, e);
        }
    }
}
