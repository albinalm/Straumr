using System.Collections.ObjectModel;
using System.Text;
using Straumr.Console.Tui.Components.ListViews;
using Straumr.Console.Tui.Components.Prompts.Base;
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

    private Label? _keyLabel;
    private InteractiveTextField? _keyField;
    private Label? _valueLabel;
    private InteractiveTextField? _valueField;
    private Button? _saveButton;
    private Label? _inputErrorLabel;

    private Mode _mode = Mode.Browsing;
    private string? _originalKey;

    public override View Build()
    {
        FrameView frame = CreateFrame(Title);
        Scheme? listScheme = BuildListScheme();
        Scheme? buttonScheme = BuildButtonScheme();

        // --- List mode views ---

        _filterLabel = new Label
        {
            Text = "Filter",
            X = 1,
            Y = 1,
        };

        _filterField = TextFieldFactory.CreateFilterField(OnFilterChanged, OnAcceptFilter, OnExitFilter);
        _filterField.X = Pos.Right(_filterLabel) + 1;
        _filterField.Y = _filterLabel.Y;
        _filterField.Width = Dim.Fill(3);

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

        Color fieldBackground = Theme != null ? ColorResolver.Resolve(Theme.Surface) : Color.Black;
        Color fieldForeground = Theme != null ? ColorResolver.Resolve(Theme.OnSurface) : Color.White;
        
        _keyField = new InteractiveTextField
        {
            X = Pos.Right(_keyLabel) + 2,
            Y = 1,
            Width = Dim.Fill(2),
            BorderStyle = LineStyle.Single,
            Visible = false,
        };
        ApplyFieldTheme(_keyField, fieldBackground, fieldForeground);

        _valueLabel = new Label
        {
            Text = "Value",
            X = 1,
            Y = 5,
            Visible = false,
        };

        _valueField = new InteractiveTextField
        {
            X = Pos.Right(_keyLabel) + 2,
            Y = 4,
            Width = Dim.Fill(2),
            BorderStyle = LineStyle.Single,
            Visible = false,
        };
        ApplyFieldTheme(_valueField, fieldBackground, fieldForeground);

        _saveButton = new Button
        {
            Text = "Save",
            X = Pos.Right(_keyLabel) + 2,
            Y = 8,
            Visible = false,
        };
        if (buttonScheme is not null)
        {
            _saveButton.SetScheme(buttonScheme);
        }

        _inputErrorLabel = new Label
        {
            X = 1,
            Y = 10,
            Width = Dim.Fill(2),
            Visible = false,
        };
        
        ConfigureEditField(_keyField, () => _saveButton, () => _valueField);
        ConfigureEditField(_valueField, () => _keyField, () => _saveButton);

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

    private bool HandleListKeyDown(Key key)
    {
        if (_listView is null)
        {
            return false;
        }

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
    
    private void ConfigureEditField(InteractiveTextField? field, Func<View?> above, Func<View?> below)
    {
        if (field is null)
        {
            return;
        }

        field.ExitEditMode();
        field.EditingStateChanged += _ => UpdateFieldIndicators();

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => !f.IsEditing && key == Key.Enter,
            (f, _) =>
            {
                f.EnterEditMode();
                return true;
            }));

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => !f.IsEditing && (key == Key.CursorUp || key.AsRune.Value == 'k'),
            (_, _) =>
            {
                FocusView(above());
                return true;
            }));

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => !f.IsEditing && (key == Key.CursorDown || key.AsRune.Value == 'j'),
            (_, _) =>
            {
                FocusView(below());
                return true;
            }));

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => !f.IsEditing && key == Key.Esc,
            (_, _) =>
            {
                ExitEditMode();
                return true;
            }));

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => f.IsEditing && key == Key.Enter,
            (f, _) =>
            {
                f.ExitEditMode();
                View? next = below();
                FocusView(next);
                if (next is InteractiveTextField nextField)
                {
                    nextField.EnterEditMode();
                }

                return true;
            }));

        field.Bind(TextFieldKeyBinding.When(
            (f, key) => f.IsEditing && key == Key.Esc,
            (f, _) =>
            {
                f.ExitEditMode();
                return true;
            }));
    }

    private void FocusView(View? view)
    {
        view?.SetFocus();
        UpdateFieldIndicators();
    }

    private void ApplyFieldTheme(InteractiveTextField field, Color background, Color foreground)
    {
        field.ApplyTheme(background, foreground);

        if (Theme is null)
        {
            return;
        }

        Color idle = ColorResolver.Resolve(Theme.Secondary);
        Color focus = ColorResolver.Resolve(Theme.Primary);
        Color edit = ColorResolver.Resolve(Theme.OnSurface);

        field.SetBorderColors(idle, focus, edit);
    }

    private void UpdateFieldIndicators()
    {
        _keyLabel?.Text = "Name";
        _valueLabel?.Text = "Value";
    }

    private void EnterEditMode(string? originalKey, string keyText, string valueText)
    {
        _mode = Mode.Editing;
        _originalKey = originalKey;

        SetListViewsVisible(false);
        SetEditViewsVisible(true);

        _keyField?.ExitEditMode();
        _valueField?.ExitEditMode();

        _keyField?.Text = keyText;

        _valueField?.Text = valueText;

        FocusView(_keyField);
        _keyField?.EnterEditMode();

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

    private void TrySave()
    {
        string key = _keyField?.Text.Trim() ?? string.Empty;
        string value = _valueField?.Text.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            ShowInputError("Name cannot be empty");
            FocusView(_keyField);
        }
        
        if (_originalKey is not null && _originalKey != key)
        {
            Items.Remove(_originalKey);
        }

        Items[key] = value;
        ExitEditMode();
    }

    private void OnFilterChanged(string text) => ApplyFilter(text);
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
        _filterLabel?.Visible = visible;

        _filterField?.Visible = visible;

        _listView?.Visible = visible;

        _emptyLabel?.Visible = visible && _keys.Count == 0;
    }

    private void SetEditViewsVisible(bool visible)
    {
        _keyLabel?.Visible = visible;

        _keyField?.Visible = visible;

        _valueLabel?.Visible = visible;

        _valueField?.Visible = visible;

        _saveButton?.Visible = visible;

        if (!visible)
        {
            HideInputError();
        }
    }

    private void ShowInputError(string message)
    {
        if (_inputErrorLabel is null)
        {
            return;
        }

        _inputErrorLabel.Text = message;
        _inputErrorLabel.Visible = true;
    }

    private void HideInputError()
    {
        _inputErrorLabel?.Visible = false;
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
