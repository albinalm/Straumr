using System.Collections.ObjectModel;
using Straumr.Console.Tui.Components.ListViews;
using Straumr.Console.Tui.Components.Prompts.Base;
using Straumr.Console.Tui.Components.Prompts.Form;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Factories;
using Straumr.Console.Tui.Helpers;
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
    public event Action? ItemSaved;
    public event Action<string>? HintsChanged;

    private readonly ObservableCollection<string> _displayItems = [];
    private readonly List<string> _keys = [];

    private SelectionListView? _listView;
    private InteractiveTextField? _filterField;
    private Label? _filterLabel;
    private Label? _emptyLabel;
    private bool _filterControlsVisible = true;

    private FormFieldsView? _editForm;

    private Mode _mode = Mode.Browsing;
    private string? _originalKey;

    public override View Build()
    {
        FrameView frame = CreateFrame(Title);
        Scheme? listScheme = BuildListScheme();

        // --- List mode views ---

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

        _editForm = new FormFieldsView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Visible = false,
            Theme = Theme,
            Fields =
            [
                new FormFieldSpec("key", "Name", Required: true),
                new FormFieldSpec("value", "Value"),
            ],
        };

        _editForm.Submitted += TrySave;
        _editForm.CancelRequested += ExitEditMode;

        frame.Add(
            _filterLabel, _filterField, _listView, _emptyLabel, _editForm);

        RebuildList();
        RefreshFilterRowLayout();

        _listView.Initialized += (_, _) => FocusView(_listView);

        return frame;
    }

    private bool HandleListKeyDown(Key key)
    {
        if (_listView is null)
        {
            return false;
        }

        int count = _keys.Count;

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
                    if (count > 0)
                    {
                        _listView.SelectedItem = 0;
                    }

                    return true;
                case 'G':
                    if (count > 0)
                    {
                        _listView.SelectedItem = count - 1;
                    }

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

        if (key == Key.S.WithCtrl)
        {
            ItemSaved?.Invoke();
            return true;
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
    

    private void EditSelected()
    {
        int? index = _listView?.SelectedItem;
        if (index is null || index < 0 || index >= _keys.Count)
        {
            return;
        }

        string key = _keys[index.Value];
        string currentValue = Items.TryGetValue(key, out string? v) ? v : string.Empty;
        EnterEditMode(key, key, currentValue);
    }

    private void DeleteSelected()
    {
        int? index = _listView?.SelectedItem;
        if (index is null || index < 0 || index >= _keys.Count)
        {
            return;
        }

        string key = _keys[index.Value];
        Items.Remove(key);
        RebuildList();

        if (_keys.Count > 0 && _listView is not null)
        {
            _listView.SelectedItem = Math.Min(index.Value, _keys.Count - 1);
        }
    }
    
    private void FocusView(View? view)
    {
        bool targetingFilter = view == _filterField;
        if (targetingFilter)
        {
            RefreshFilterRowLayout(forceVisible: true);
        }

        view?.SetFocus();
        RefreshFilterRowLayout(targetingFilter);
    }

    private void ApplyFilterFieldTheme(InteractiveTextField field)
    {
        string background = Theme?.Surface ?? "Black";
        string foreground = Theme?.OnSurface ?? "White";
        field.ApplyTheme(ColorResolver.Resolve(background), ColorResolver.Resolve(foreground));
    }

    private void RefreshFilterRowLayout(bool forceVisible = false)
    {
        if (_filterLabel is null || _filterField is null)
        {
            return;
        }

        string text = _filterField.Text?.ToString() ?? string.Empty;
        bool hasText = text.Length > 0;
        bool hasFocus = _filterField.HasFocus;
        bool shouldShow = _filterControlsVisible && (forceVisible || hasText || hasFocus);

        _filterLabel.Visible = shouldShow;
        _filterField.Visible = shouldShow;

        _listView?.Y = shouldShow ? 3 : 1;

        _emptyLabel?.Y = shouldShow ? 3 : 1;
    }

    private void EnterEditMode(string? originalKey, string keyText, string valueText)
    {
        _mode = Mode.Editing;
        _originalKey = originalKey;

        SetListViewsVisible(false);
        SetEditViewsVisible(true);

        _editForm?.SetValues(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["key"] = keyText,
            ["value"] = valueText,
        });
        _editForm?.FocusFirstField();
        UpdateHints();
    }

    private void ExitEditMode()
    {
        _mode = Mode.Browsing;
        _originalKey = null;

        SetEditViewsVisible(false);
        SetListViewsVisible(true);

        RebuildList();
        UpdateHints();

        FocusView(_listView);
    }

    private void TrySave(Dictionary<string, string> values)
    {
        string key = values.TryGetValue("key", out string? keyValue) ? keyValue.Trim() : string.Empty;
        string value = values.TryGetValue("value", out string? valueValue) ? valueValue.Trim() : string.Empty;

        if (_originalKey is not null && _originalKey != key)
        {
            Items.Remove(_originalKey);
        }

        Items[key] = value;
        ExitEditMode();
    }

    private void OnFilterChanged(string text)
    {
        ApplyFilter(text);
        RefreshFilterRowLayout();
    }
    private void OnAcceptFilter() => FocusView(_listView);
    private void OnExitFilter() => FocusView(_listView);

    private void RebuildList()
    {
        _displayItems.Clear();
        _keys.Clear();

        foreach (KeyValuePair<string, string> kvp in Items.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            _keys.Add(kvp.Key);
            _displayItems.Add(FormatEntry(kvp.Key, kvp.Value));
        }

        bool hasItems = _keys.Count > 0;
        _listView?.SelectedItem = hasItems ? 0 : null;

        _emptyLabel?.Visible = !hasItems;
    }

    private void ApplyFilter(string filter)
    {
        _displayItems.Clear();
        _keys.Clear();

        foreach (KeyValuePair<string, string> kvp in Items.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            string display = FormatEntry(kvp.Key, kvp.Value);
            if (!string.IsNullOrEmpty(filter)
                && !display.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _keys.Add(kvp.Key);
            _displayItems.Add(display);
        }

        bool hasItems = _keys.Count > 0;
        _listView?.SelectedItem = hasItems ? 0 : null;

        if (_emptyLabel is not null)
        {
            _emptyLabel.Text = string.IsNullOrEmpty(filter)
                ? "(empty) Press 'a' to add an entry"
                : "No results";
            _emptyLabel.Visible = !hasItems;
        }

        RefreshFilterRowLayout();
    }

    private void MoveSelection(int delta)
    {
        if (_listView is null || _keys.Count == 0)
        {
            return;
        }

        int current = _listView.SelectedItem ?? 0;
        int next = Math.Clamp(current + delta, 0, _keys.Count - 1);
        _listView.SelectedItem = next;
    }

    private void SetListViewsVisible(bool visible)
    {
        _filterControlsVisible = visible;

        _listView?.Visible = visible;

        _emptyLabel?.Visible = visible && _keys.Count == 0;

        RefreshFilterRowLayout();
    }

    private void SetEditViewsVisible(bool visible)
    {
        _editForm?.Visible = visible;
    }

    private void UpdateHints()
    {
        HintsChanged?.Invoke(_mode == Mode.Browsing ? BrowseHints : InputHints);
    }

    private static string FormatEntry(string key, string value)
        => $"{key} = {value}";

    internal const string BrowseHints = "j/k Navigate  a Add  e Edit  d Delete  / Filter  :s Save  Esc Done";
    internal const string InputHints = "Enter Edit/Next  j/k Navigate  Esc Back";
}
