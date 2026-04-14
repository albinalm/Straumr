using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
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

    public int MaxItemLength
    {
        get
        {
            int max = 0;
            foreach (MarkupLabel item in _items)
            {
                int len = item.PlainMaxLineLength;
                if (len > max)
                {
                    max = len;
                }
            }

            return max;
        }
    }

    public bool SuspendCollectionChangedEvent { get; set; }

    public void RaiseReset()
        => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    public bool IsMarked(int item) => false;

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX)
    {
        Scheme scheme = listView.GetScheme();
        TuiAttribute normalAttr = scheme.Normal;

        int contentRow = row + listView.Viewport.Y;
        if (contentRow >= _items.Count * RowsPerItem)
        {
            for (var fx = 0; fx < width; fx++)
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

        IReadOnlyList<MarkupText.MarkupRun> lineRuns = Array.Empty<MarkupText.MarkupRun>();
        if (logicalIndex >= 0 && logicalIndex < _items.Count)
        {
            IReadOnlyList<IReadOnlyList<MarkupText.MarkupRun>> parsedLines = _items[logicalIndex].ParsedLines;
            if (lineIndex < parsedLines.Count)
            {
                lineRuns = parsedLines[lineIndex];
            }
        }

        TuiAttribute rowBase = isSelected
            ? scheme.Focus
            : normalAttr;

        bool replaceGlyph = isSelected && lineIndex == 0;
        var x = 0;
        var currentColumn = 0;

        foreach (MarkupText.MarkupRun run in lineRuns)
        {
            TuiAttribute attribute = isSelected
                ? new TuiAttribute(rowBase.Foreground, rowBase.Background, run.Attribute.Style)
                : run.Attribute;
            listView.SetAttribute(attribute);

            foreach (Rune rune in run.Text.EnumerateRunes())
            {
                Rune outRune = rune;
                if (replaceGlyph && rune.Value == '◇')
                {
                    outRune = new Rune('▸');
                    replaceGlyph = false;
                }

                if (currentColumn++ < viewportX)
                {
                    continue;
                }

                if (x >= width)
                {
                    break;
                }

                listView.AddRune(col + x, row, outRune);
                x++;
            }

            if (x >= width)
            {
                break;
            }
        }

        if (x < width)
        {
            listView.SetAttribute(rowBase);
            for (; x < width; x++)
            {
                listView.AddRune(col + x, row, new Rune(' '));
            }
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

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!SuspendCollectionChangedEvent)
        {
            CollectionChanged?.Invoke(sender, e);
        }
    }
}
