using System.Collections.ObjectModel;
using System.Text;
using Straumr.Console.Tui.Components.Prompts.Selection;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.KeyValue;

internal sealed class KeyValueEditorComponent : PromptComponent
{
    private enum Mode { Browsing, Editing }

    public required string Title { get; init; }
    public required IDictionary<string, string> Items { get; init; }

    public event Action? DoneRequested;
    public event Action<string>? HintsChanged;

    private readonly ObservableCollection<string> _displayItems = [];
    private readonly List<string> _keys = [];

    private SelectionListView? _listView;
    private FilterTextField? _filterField;
    private Label? _filterLabel;
    private Label? _emptyLabel;

    private Label? _keyLabel;
    private EditFormField? _keyField;
    private Label? _valueLabel;
    private EditFormField? _valueField;
    private Button? _saveButton;
    private Label? _inputErrorLabel;

    private Mode _mode = Mode.Browsing;
    private string? _originalKey;

    public override View Build()
    {
        FrameView frame = CreateFrame(Title);
        Scheme? listScheme = BuildListScheme();
        Scheme? inputScheme = BuildInputScheme();
        Scheme? editingScheme = BuildInputEditingScheme();

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

        _listView = new SelectionListView(HandleListKeyDown, listScheme)
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

        // --- Edit mode views ---

        _keyLabel = new Label
        {
            Text = "Name",
            X = 1,
            Y = 2,
            Visible = false,
        };

        _keyField = new EditFormField
        {
            X = Pos.Right(_keyLabel) + 2,
            Y = 1,
            Width = Dim.Fill(2),
            BorderStyle = LineStyle.Single,
            Visible = false,
            DisplayScheme = inputScheme,
            EditingScheme = editingScheme,
            DisplayBorderStyle = LineStyle.Single,
            EditingBorderStyle = LineStyle.Double,
        };

        _valueLabel = new Label
        {
            Text = "Value",
            X = 1,
            Y = 5,
            Visible = false,
        };

        _valueField = new EditFormField
        {
            X = Pos.Right(_keyLabel) + 2,
            Y = 4,
            Width = Dim.Fill(2),
            BorderStyle = LineStyle.Single,
            Visible = false,
            DisplayScheme = inputScheme,
            EditingScheme = editingScheme,
            DisplayBorderStyle = LineStyle.Single,
            EditingBorderStyle = LineStyle.Double,
        };

        _saveButton = new Button
        {
            Text = "Save",
            X = Pos.Right(_keyLabel) + 2,
            Y = 8,
            Visible = false,
        };

        _inputErrorLabel = new Label
        {
            X = 1,
            Y = 10,
            Width = Dim.Fill(2),
            Visible = false,
        };

        // Wire up edit form navigation and theming
        WireEditFormField(_keyField, () => _saveButton, () => _valueField);
        WireEditFormField(_valueField, () => _keyField, () => _saveButton);

        _saveButton.Accepting += (_, _) => TrySave();
        _saveButton.KeyDown += (_, key) =>
        {
            Rune rune = key.AsRune;
            bool up = key == Key.CursorUp || rune.Value == 'k';
            bool down = key == Key.CursorDown || rune.Value == 'j';

            if (up)
            {
                key.Handled = true;
                FocusView(_valueField);
            }
            else if (down)
            {
                key.Handled = true;
                FocusView(_keyField);
            }
            else if (key == Key.Esc)
            {
                key.Handled = true;
                ExitEditMode();
            }
        };

        frame.Add(
            _filterLabel, _filterField, _listView, _emptyLabel,
            _keyLabel, _keyField, _valueLabel, _valueField, _saveButton, _inputErrorLabel);

        RebuildList();

        _listView.Initialized += (_, _) => FocusView(_listView);
        UpdateFieldIndicators();
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
                    FocusView(_filterField);
                    return true;
                case 'a':
                    EnterEditMode(null, string.Empty, string.Empty);
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
        EnterEditMode(key, key, currentValue);
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

    // --- Edit mode ---

    private void WireEditFormField(EditFormField field, Func<View?> above, Func<View?> below)
    {
        field.EditRequested += () => field.EnterEditMode();
        field.EditCompleted += () =>
        {
            field.ExitEditMode();
            FocusView(below());
        };
        field.EditCancelled += () => field.ExitEditMode();
        field.NavigateUp += () => FocusView(above());
        field.NavigateDown += () => FocusView(below());
        field.ExitRequested += ExitEditMode;
        field.EditingStateChanged += UpdateFieldIndicators;
    }

    private void FocusView(View? view)
    {
        view?.SetFocus();
        UpdateFieldIndicators();
    }

    private void UpdateFieldIndicators()
    {
        UpdateFieldIndicator(_keyField, _keyLabel, "Name");
        UpdateFieldIndicator(_valueField, _valueLabel, "Value");
    }

    private static void UpdateFieldIndicator(EditFormField? field, Label? label, string labelText)
    {
        if (label is null)
            return;

        string suffix = string.Empty;
        if (field?.IsEditing == true)
            suffix = " (editing)";
        else if (field?.HasFocus == true)
            suffix = " (selected)";

        label.Text = string.IsNullOrEmpty(suffix) ? labelText : $"{labelText}{suffix}";
    }

    private void EnterEditMode(string? originalKey, string keyText, string valueText)
    {
        _mode = Mode.Editing;
        _originalKey = originalKey;

        SetListViewsVisible(false);
        SetEditViewsVisible(true);

        _keyField?.ExitEditMode();
        _valueField?.ExitEditMode();

        if (_keyField is not null)
            _keyField.Text = keyText;

        if (_valueField is not null)
            _valueField.Text = valueText;

        // Focus the key field for new entries, value field when editing existing
        if (originalKey is null)
            FocusView(_keyField);
        else
            FocusView(_valueField);

        HideInputError();
        UpdateHints();
    }

    private void ExitEditMode()
    {
        _keyField?.ExitEditMode();
        _valueField?.ExitEditMode();

        _mode = Mode.Browsing;
        _originalKey = null;

        SetEditViewsVisible(false);
        SetListViewsVisible(true);

        RebuildList();
        UpdateHints();

        FocusView(_listView);
    }

    private bool TrySave()
    {
        string key = _keyField?.Text?.Trim() ?? string.Empty;
        string value = _valueField?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            ShowInputError("Name cannot be empty");
            FocusView(_keyField);
            return true;
        }

        // If renaming, remove the old key
        if (_originalKey is not null && _originalKey != key)
            Items.Remove(_originalKey);

        Items[key] = value;
        ExitEditMode();
        return true;
    }

    // --- Filter callbacks ---

    private void OnFilterChanged(string text) => ApplyFilter(text);
    private void OnAcceptFilter() => FocusView(_listView);
    private void OnExitFilter() => FocusView(_listView);

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

    private void SetEditViewsVisible(bool visible)
    {
        if (_keyLabel is not null) _keyLabel.Visible = visible;
        if (_keyField is not null) _keyField.Visible = visible;
        if (_valueLabel is not null) _valueLabel.Visible = visible;
        if (_valueField is not null) _valueField.Visible = visible;
        if (_saveButton is not null) _saveButton.Visible = visible;
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
        HintsChanged?.Invoke(_mode == Mode.Browsing ? BrowseHints : InputHints);
    }

    private static string FormatEntry(string key, string value)
        => $"{key} = {value}";

    internal const string BrowseHints = "j/k Navigate  a Add  e Edit  d Delete  / Filter  Esc Done";
    internal const string InputHints = "Enter Edit/Next  j/k Navigate  Esc Back";
}
