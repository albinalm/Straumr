using System.Collections.ObjectModel;
using System.Text;
using Straumr.Console.Tui.Components.Prompts.Selection;
using Straumr.Console.Tui.Components.Prompts.TextInput;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.KeyValue;

internal sealed class KeyValueEditorComponent : PromptComponent
{
    private enum Mode { Browsing, EnteringName, EnteringValue }

    public required string Title { get; init; }
    public required IDictionary<string, string> Items { get; init; }

    public event Action? DoneRequested;

    private readonly ObservableCollection<string> _displayItems = [];
    private readonly List<string> _keys = [];

    private SelectionListView? _listView;
    private FilterTextField? _filterField;
    private Label? _filterLabel;
    private Label? _emptyLabel;
    private Label? _hintsLabel;

    private Label? _inputPromptLabel;
    private PromptTextField? _inputField;
    private Label? _inputErrorLabel;

    private Mode _mode = Mode.Browsing;
    private string? _pendingKey;

    public override View Build()
    {
        FrameView frame = CreateFrame(Title);

        // --- List mode views ---

        _filterLabel = new Label
        {
            Text = "Filter",
            X = 1,
            Y = 1,
        };

        _filterField = new FilterTextField(OnFilterChanged, OnAcceptFilter, OnExitFilter, () => DoneRequested?.Invoke())
        {
            X = Pos.Right(_filterLabel) + 1,
            Y = _filterLabel.Y,
            Width = Dim.Fill(3),
        };

        _listView = new SelectionListView(HandleListKeyDown)
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
        };

        _listView.SetSource(_displayItems);

        _emptyLabel = new Label
        {
            Text = "(empty) Press 'a' to add an entry",
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Visible = false,
        };

        // --- Input mode views ---

        _inputPromptLabel = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Visible = false,
        };

        _inputField = new PromptTextField(OnInputTextChanged, OnInputSubmit, OnInputCancel)
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Visible = false,
        };

        _inputErrorLabel = new Label
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill(2),
            Visible = false,
        };

        // --- Hints ---

        _hintsLabel = new Label
        {
            Text = BrowseHints,
            X = 1,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(2),
        };

        frame.Add(
            _filterLabel, _filterField, _listView, _emptyLabel,
            _inputPromptLabel, _inputField, _inputErrorLabel,
            _hintsLabel);

        RebuildList();

        _listView.Initialized += (_, _) => _listView.SetFocus();

        return frame;
    }

    // --- Browse mode key handling ---

    private bool HandleListKeyDown(Key key)
    {
        if (_listView is null)
            return false;

        int count = _keys.Count;

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
                    if (count > 0) _listView.SelectedItem = 0;
                    return true;
                case 'G':
                    if (count > 0) _listView.SelectedItem = count - 1;
                    return true;
                case '/':
                    _filterField?.SetFocus();
                    return true;
                case 'a':
                    EnterInputMode(Mode.EnteringName, "Name:", string.Empty);
                    return true;
                case 'e':
                    EditSelected();
                    return true;
                case 'd':
                    DeleteSelected();
                    return true;
            }
        }

        if (key == Key.Enter)
        {
            EditSelected();
            return true;
        }

        if (key == Key.Esc)
        {
            DoneRequested?.Invoke();
            return true;
        }

        return false;
    }

    // --- CRUD operations ---

    private void EditSelected()
    {
        int? index = _listView?.SelectedItem;
        if (index is null || index < 0 || index >= _keys.Count)
            return;

        string key = _keys[index.Value];
        string currentValue = Items.TryGetValue(key, out string? v) ? v : string.Empty;
        _pendingKey = key;
        EnterInputMode(Mode.EnteringValue, FormatValuePrompt(key, currentValue), currentValue);
    }

    private void DeleteSelected()
    {
        int? index = _listView?.SelectedItem;
        if (index is null || index < 0 || index >= _keys.Count)
            return;

        string key = _keys[index.Value];
        Items.Remove(key);
        RebuildList();

        if (_keys.Count > 0 && _listView is not null)
        {
            _listView.SelectedItem = Math.Min(index.Value, _keys.Count - 1);
        }
    }

    // --- Input mode ---

    private void EnterInputMode(Mode mode, string prompt, string initialValue)
    {
        _mode = mode;
        if (mode == Mode.EnteringName)
            _pendingKey = null;

        SetListViewsVisible(false);
        SetInputViewsVisible(true);

        if (_inputPromptLabel is not null)
            _inputPromptLabel.Text = prompt;

        if (_inputField is not null)
        {
            _inputField.Text = initialValue;
            _inputField.SetFocus();
        }

        HideInputError();
        UpdateHints();
    }

    private void ExitInputMode()
    {
        _mode = Mode.Browsing;
        _pendingKey = null;

        SetInputViewsVisible(false);
        SetListViewsVisible(true);

        RebuildList();
        UpdateHints();

        _listView?.SetFocus();
    }

    private bool OnInputSubmit()
    {
        string value = _inputField?.Text?.Trim() ?? string.Empty;

        if (_mode == Mode.EnteringName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ShowInputError("Name cannot be empty");
                return true;
            }

            _pendingKey = value;
            string existing = Items.TryGetValue(value, out string? v) ? v : string.Empty;
            EnterInputMode(Mode.EnteringValue, FormatValuePrompt(value, existing), existing);
            return true;
        }

        if (_mode == Mode.EnteringValue && _pendingKey is not null)
        {
            Items[_pendingKey] = value;
            ExitInputMode();
            return true;
        }

        return true;
    }

    private bool OnInputCancel()
    {
        ExitInputMode();
        return true;
    }

    private void OnInputTextChanged()
    {
        HideInputError();
    }

    // --- Filter callbacks ---

    private void OnFilterChanged(string text) => ApplyFilter(text);
    private void OnAcceptFilter() => _listView?.SetFocus();
    private void OnExitFilter() => _listView?.SetFocus();

    // --- List management ---

    private void RebuildList()
    {
        _displayItems.Clear();
        _keys.Clear();

        foreach (var kvp in Items.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            _keys.Add(kvp.Key);
            _displayItems.Add(FormatEntry(kvp.Key, kvp.Value));
        }

        bool hasItems = _keys.Count > 0;
        if (_listView is not null)
            _listView.SelectedItem = hasItems ? 0 : null;

        if (_emptyLabel is not null)
            _emptyLabel.Visible = !hasItems;
    }

    private void ApplyFilter(string filter)
    {
        _displayItems.Clear();
        _keys.Clear();

        foreach (var kvp in Items.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            string display = FormatEntry(kvp.Key, kvp.Value);
            if (!string.IsNullOrEmpty(filter)
                && !display.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            _keys.Add(kvp.Key);
            _displayItems.Add(display);
        }

        bool hasItems = _keys.Count > 0;
        if (_listView is not null)
            _listView.SelectedItem = hasItems ? 0 : null;

        if (_emptyLabel is not null)
        {
            _emptyLabel.Text = string.IsNullOrEmpty(filter)
                ? "(empty) Press 'a' to add an entry"
                : "No results";
            _emptyLabel.Visible = !hasItems;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_listView is null || _keys.Count == 0)
            return;

        int current = _listView.SelectedItem ?? 0;
        int next = Math.Clamp(current + delta, 0, _keys.Count - 1);
        _listView.SelectedItem = next;
    }

    // --- View visibility helpers ---

    private void SetListViewsVisible(bool visible)
    {
        if (_filterLabel is not null) _filterLabel.Visible = visible;
        if (_filterField is not null) _filterField.Visible = visible;
        if (_listView is not null) _listView.Visible = visible;
        if (_emptyLabel is not null) _emptyLabel.Visible = visible && _keys.Count == 0;
    }

    private void SetInputViewsVisible(bool visible)
    {
        if (_inputPromptLabel is not null) _inputPromptLabel.Visible = visible;
        if (_inputField is not null) _inputField.Visible = visible;
        if (!visible) HideInputError();
    }

    private void ShowInputError(string message)
    {
        if (_inputErrorLabel is null) return;
        _inputErrorLabel.Text = message;
        _inputErrorLabel.Visible = true;
    }

    private void HideInputError()
    {
        if (_inputErrorLabel is null) return;
        _inputErrorLabel.Visible = false;
    }

    private void UpdateHints()
    {
        if (_hintsLabel is null) return;
        _hintsLabel.Text = _mode == Mode.Browsing ? BrowseHints : InputHints;
    }

    private static string FormatEntry(string key, string value)
        => $"{key} = {value}";

    private static string FormatValuePrompt(string key, string currentValue)
        => string.IsNullOrEmpty(currentValue)
            ? $"Value for '{key}':"
            : $"Value for '{key}' (current: {currentValue}):";

    private const string BrowseHints = "j/k Navigate  a Add  e Edit  d Delete  / Filter  Esc Done";
    private const string InputHints = "Enter Confirm  Esc Cancel";
}
