using System.Collections.ObjectModel;
using System.Text;
using Straumr.Console.Tui.Components.ListViews;
using Straumr.Console.Tui.Components.Prompts.Base;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Factories;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drivers;
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
    public bool EnableFilter { get; init; } = true;
    public bool EnableTypeahead { get; init; }

    public event Action<string>? SelectionAccepted;
    public event Action? CancelRequested;

    private readonly ObservableCollection<string> _displayItems = [];
    private readonly List<string> _valueItems = [];
    private List<(string Value, string Display)> _sourceItems = [];
    private ListView? _listView;
    private InteractiveTextField? _filterField;
    private Label? _filterLabel;
    private Label? _emptyLabel;
    private string _typeaheadBuffer = string.Empty;
    private DateTimeOffset _lastTypeahead;

    public override View Build()
    {
        _sourceItems = Items.Select(value => (value, FormatDisplay(value))).ToList();

        FrameView frame = CreateFrame(Title);

        _listView = new SelectionListView(HandleListKeyDown, BuildListScheme())
        {
            X = 1,
            Y = EnableFilter ? 3 : 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(EnableFilter ? 2 : 1),
        };

        _listView.SetSource(_displayItems);
        _listView.Accepting += (_, _) => AcceptSelection();

        _emptyLabel = new Label
        {
            Text = "No results",
            X = 1,
            Y = EnableFilter ? 3 : 1,
            Width = Dim.Fill(2),
            Visible = false,
        };

        if (EnableFilter)
        {
            _filterLabel = new Label
            {
                Text = "Filter:",
                X = 1,
                Y = 1,
            };

            _filterField = TextFieldFactory.CreateFilterField(OnFilterChanged, OnAcceptFilter, OnExitFilter);
            _filterField.X = Pos.Right(_filterLabel) + 1;
            _filterField.Y = _filterLabel.Y;
            _filterField.Width = Dim.Fill(3);
            ApplyFilterFieldTheme(_filterField);

            frame.Add(_filterLabel, _filterField);
        }

        frame.Add(_listView, _emptyLabel);
        ApplyFilter(string.Empty);
        RefreshFilterRowLayout();

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
        if (count == 0 && !KeyHelpers.IsEscape(key) && KeyHelpers.GetCharValue(key) != '/')
        {
            return false;
        }

        if (!key.IsCtrl && !key.IsAlt)
        {
            switch (KeyHelpers.GetCharValue(key))
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
                    if (EnableFilter)
                    {
                        FocusFilter();
                        return true;
                    }

                    return true;
            }
        }

        if (EnableTypeahead && !key.IsCtrl && !key.IsAlt && HandleTypeahead(key))
        {
            return true;
        }

        if (KeyHelpers.IsEscape(key))
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
        RefreshFilterRowLayout(forceVisible: true);
        _filterField?.SetFocus();
    }

    private void ApplyFilterFieldTheme(InteractiveTextField field)
    {
        string background = Theme?.Surface ?? "Black";
        string foreground = Theme?.OnSurface ?? "White";
        field.ApplyTheme(ColorResolver.Resolve(background), ColorResolver.Resolve(foreground));
    }

    private void RefreshFilterRowLayout(bool forceVisible = false)
    {
        if (!EnableFilter || _filterLabel is null || _filterField is null)
        {
            return;
        }

        string text = _filterField.Text?.ToString() ?? string.Empty;
        bool hasText = text.Length > 0;
        bool hasFocus = _filterField.HasFocus;
        bool shouldShow = forceVisible || hasText || hasFocus;

        _filterLabel.Visible = shouldShow;
        _filterField.Visible = shouldShow;

        int listOffset = shouldShow ? 3 : 1;
        _listView?.Y = listOffset;

        _emptyLabel?.Y = listOffset;
    }

    private void FocusList()
    {
        _listView?.SetFocus();
        RefreshFilterRowLayout();
    }

    private void OnAcceptFilter()
    {
        FocusList();
    }

    private void OnExitFilter()
    {
        FocusList();
    }

    private void OnFilterChanged(string text)
    {
        ApplyFilter(text);
        RefreshFilterRowLayout();
    }

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
        RefreshFilterRowLayout();
    }

    private bool HandleTypeahead(Key key)
    {
        if (_listView is null || _displayItems.Count == 0)
        {
            return false;
        }

        if (key == Key.Backspace || key == Key.Delete)
        {
            if (_typeaheadBuffer.Length == 0)
            {
                return false;
            }

            _typeaheadBuffer = _typeaheadBuffer[..^1];
            return MoveSelectionToTypeahead();
        }

        if (!TryGetTypeaheadChar(key, out char ch))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow - _lastTypeahead > TimeSpan.FromSeconds(1))
        {
            _typeaheadBuffer = string.Empty;
        }

        _lastTypeahead = DateTimeOffset.UtcNow;
        _typeaheadBuffer += ch;

        return MoveSelectionToTypeahead();
    }

    private static bool TryGetTypeaheadChar(Key key, out char ch)
    {
        Rune rune = key.AsRune;
        if (rune.Value != 0)
        {
            var candidate = (char)rune.Value;
            if (char.IsLetterOrDigit(candidate) || candidate == ' ')
            {
                ch = candidate;
                return true;
            }
        }

        KeyCode keyCode = key.KeyCode;
        var keyValue = (int)keyCode;

        if (keyValue is >= (int)KeyCode.A and <= (int)KeyCode.Z)
        {
            ch = (char)('a' + (keyValue - (int)KeyCode.A));
            return true;
        }

        if (keyValue is >= (int)KeyCode.D0 and <= (int)KeyCode.D9)
        {
            ch = (char)('0' + (keyValue - (int)KeyCode.D0));
            return true;
        }

        if (keyCode == KeyCode.Space)
        {
            ch = ' ';
            return true;
        }

        ch = '\0';
        return false;
    }

    private bool MoveSelectionToTypeahead()
    {
        if (_listView is null)
        {
            return false;
        }

        string filter = _typeaheadBuffer;
        if (string.IsNullOrEmpty(filter))
        {
            return false;
        }

        int index = -1;
        for (int i = 0; i < _displayItems.Count; i++)
        {
            if (_displayItems[i].StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
        {
            _listView.SelectedItem = index;
            return true;
        }
        
        if (_typeaheadBuffer.Length > 0)
        {
            char last = _typeaheadBuffer[^1];
            _typeaheadBuffer = last.ToString();
            for (var i = 0; i < _displayItems.Count; i++)
            {
                if (_displayItems[i].StartsWith(_typeaheadBuffer, StringComparison.OrdinalIgnoreCase))
                {
                    _listView.SelectedItem = i;
                    return true;
                }
            }
        }

        return false;
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
