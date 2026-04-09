using System.Collections.ObjectModel;
using System.Text;
using Straumr.Console.Tui.Components.ListViews;
using Straumr.Console.Tui.Components.Prompts.Base;
using Straumr.Console.Tui.Components.TextFields;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using MarkupText = Straumr.Console.Tui.Helpers.MarkupText;

namespace Straumr.Console.Tui.Components.Prompts.Selection;

internal sealed class SelectionPrompt : PromptComponent
{
    public required string Title { get; init; }
    public required IReadOnlyList<string> Items { get; init; }
    public Func<string, string>? DisplayConverter { get; init; }

    public event Action<string>? SelectionAccepted;
    public event Action? CancelRequested;

    private readonly ObservableCollection<string> _displayItems = [];
    private readonly List<string> _valueItems = [];
    private List<(string Value, string Display)> _sourceItems = [];
    private ListView? _listView;
    private FilterTextField? _filterField;
    private Label? _emptyLabel;

    public override View Build()
    {
        _sourceItems = Items.Select(value => (value, FormatDisplay(value))).ToList();

        FrameView frame = CreateFrame(Title);

        Label filterLabel = new()
        {
            Text = "Filter",
            X = 1,
            Y = 1,
        };

        _filterField = new FilterTextField(OnFilterChanged, OnAcceptFilter, OnExitFilter)
        {
            X = Pos.Right(filterLabel) + 1,
            Y = filterLabel.Y,
            Width = Dim.Fill(3),
        };

        _listView = new SelectionListView(HandleListKeyDown, BuildListScheme())
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
        };

        _listView.SetSource(_displayItems);
        _listView.Accepting += (_, _) => AcceptSelection();

        _emptyLabel = new Label
        {
            Text = "No results",
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Visible = false,
        };

        frame.Add(filterLabel, _filterField, _listView, _emptyLabel);
        ApplyFilter(string.Empty);

        // Defer focus until the view is attached to the window
        _listView.Initialized += (_, _) => _listView.SetFocus();

        return frame;
    }

    private bool HandleListKeyDown(Key key)
    {
        if (_listView is null)
        {
            return false;
        }

        int count = _valueItems.Count;
        if (count == 0 && key != Key.Esc && key.AsRune.Value != '/')
        {
            return false;
        }

        // VIM motions
        Rune rune = key.AsRune;

        if (!key.IsCtrl && !key.IsAlt)
        {
            switch (rune.Value)
            {
                case 'j':
                    MoveSelection(1);
                    return true;
                case 'k':
                    MoveSelection(-1);
                    return true;
                case 'g':
                    _listView.SelectedItem = 0;
                    return true;
                case 'G':
                    _listView.SelectedItem = count - 1;
                    return true;
                case '/':
                    FocusFilter();
                    return true;
            }
        }

        if (key == Key.Esc)
        {
            CancelRequested?.Invoke();
            return true;
        }

        return false;
    }

    private void MoveSelection(int delta)
    {
        if (_listView is null || _valueItems.Count == 0)
        {
            return;
        }

        int current = _listView.SelectedItem ?? 0;
        int next = Math.Clamp(current + delta, 0, _valueItems.Count - 1);
        _listView.SelectedItem = next;
    }

    private void FocusFilter()
    {
        _filterField?.SetFocus();
    }

    private void FocusList()
    {
        _listView?.SetFocus();
    }

    private void OnAcceptFilter()
    {
        FocusList();
    }

    private void OnExitFilter()
    {
        FocusList();
    }

    private void OnFilterChanged(string text) => ApplyFilter(text);

    private void ApplyFilter(string filter)
    {
        _displayItems.Clear();
        _valueItems.Clear();

        foreach ((string value, string display) in _sourceItems)
        {
            if (!MatchesFilter(display, filter))
            {
                continue;
            }

            _displayItems.Add(display);
            _valueItems.Add(value);
        }

        bool hasItems = _valueItems.Count > 0;
        if (_listView is not null)
        {
            _listView.SelectedItem = hasItems ? 0 : null;
            _listView.Visible = hasItems;
        }

        _emptyLabel?.Visible = !hasItems;
    }

    private bool MatchesFilter(string display, string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        return display.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private string FormatDisplay(string value)
    {
        string output = DisplayConverter?.Invoke(value) ?? value;
        return MarkupText.ToPlain(output);
    }

    private void AcceptSelection()
    {
        if (_listView?.SelectedItem is null)
        {
            return;
        }

        int index = _listView.SelectedItem.Value;
        if (index < 0 || index >= _valueItems.Count)
        {
            return;
        }

        SelectionAccepted?.Invoke(_valueItems[index]);
    }
}
