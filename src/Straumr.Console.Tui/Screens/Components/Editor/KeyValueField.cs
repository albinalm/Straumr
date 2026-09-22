using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class KeyValueField : EditorField
{
    private readonly string _entryName;
    private readonly IDictionary<string, string> _items;
    private readonly KeyValueValueKind _kind;
    private readonly ResourceList _list;
    private readonly State<int> _selectedIndex = new(-1);
    private List<string> _keys = [];

    public KeyValueField(
        string label,
        string entryName,
        IDictionary<string, string> items,
        KeyValueValueKind kind = KeyValueValueKind.Text)
        : base(label)
    {
        _items = items;
        _entryName = entryName;
        _kind = kind;
        GrowsToFill = true;

        _list = new ResourceList([], ResourceScreenLayoutHelpers.Message(
                new TextBlock($"No {entryName}s. Press {TuiKeybindHelpers.Hint("KeyValueField.Add")} to add one.")
                    .Style(StraumrStyleService.MutedText)
                    .Wrap(true)
                    .Trimming(TextTrimming.EndEllipsis)),
            "Edit");
        _list.BindSelectedIndex(_selectedIndex);
        _list.ItemActivated += _ => Edit(TuiKeybindHelpers.OpeningEcho);
        _list.AddCommand(Action("Add", Add, () => true));
        _list.AddCommand(Action("Edit", Edit, () => Selected is not null));
        _list.AddCommand(Action("Remove", _ => Remove(), () => Selected is not null));

        Refresh(null);
        Content = ResourceScreenLayoutHelpers.Scrollable(_list);
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _list;

    private string? Selected =>
        (uint)_selectedIndex.Value < (uint)_keys.Count ? _keys[_selectedIndex.Value] : null;

    public void Reload() => Refresh(Selected);

    private void Add(char? echo)
    {
        var dialog = new KeyValuePairDialog($"Add {_entryName}", "Name", "Value",
            string.Empty, string.Empty, "Add",
            key => Validate(key, null), (key, value) =>
            {
                _items[key] = value;
                Refresh(key);
                Changed?.Invoke();
            }, _kind);
        dialog.PendingEcho = echo;
        dialog.Show();
    }

    private void Edit(char? echo)
    {
        if (Selected is not { } key)
        {
            return;
        }

        var dialog = new KeyValuePairDialog($"Edit {_entryName}", "Name", "Value", key, _items[key], "Save",
            candidate => Validate(candidate, key), (candidate, value) =>
            {
                if (!string.Equals(candidate, key, StringComparison.Ordinal))
                {
                    _items.Remove(key);
                }

                _items[candidate] = value;
                Refresh(candidate);
                Changed?.Invoke();
            }, _kind);
        dialog.PendingEcho = echo;
        dialog.Show();
    }

    private void Remove()
    {
        if (Selected is not { } key)
        {
            return;
        }

        int index = _keys.IndexOf(key);
        _items.Remove(key);
        Refresh(null);
        _selectedIndex.Value = _keys.Count == 0 ? -1 : Math.Clamp(index, 0, _keys.Count - 1);
        Changed?.Invoke();
    }

    private string? Validate(string key, string? replacing)
    {
        if (key.Length == 0)
        {
            return "Name cannot be empty.";
        }

        if (replacing is not null && KeysEqual(key, replacing))
        {
            return null;
        }

        return _items.ContainsKey(key) ? $"A {_entryName} named {key} already exists." : null;
    }

    private bool KeysEqual(string left, string right) =>
        _items is Dictionary<string, string> map
            ? map.Comparer.Equals(left, right)
            : string.Equals(left, right, StringComparison.Ordinal);

    private void Refresh(string? select)
    {
        _keys = _items.Keys.ToList();
        _list.SetRows(_keys.Select(key => new ResourceRowModel(
            key,
            Describe(_items[key]),
            _items[key].Length > 0,
            IsBroken: IsMissingFile(_items[key]))));

        int index = select is null ? -1 : _keys.IndexOf(select);
        if (index >= 0)
        {
            _selectedIndex.Value = index;
        }
        else if (_selectedIndex.Value >= _keys.Count)
        {
            _selectedIndex.Value = _keys.Count - 1;
        }
        else if (_selectedIndex.Value < 0 && _keys.Count > 0)
        {
            _selectedIndex.Value = 0;
        }
    }

    private string Describe(string value)
    {
        if (value.Length == 0)
        {
            return "empty";
        }

        if (!IsFile(value))
        {
            return value;
        }

        string path = value[1..];
        return File.Exists(path)
            ? $"file · {Path.GetFileName(path)}"
            : $"file missing · {Path.GetFileName(path)}";
    }

    private bool IsMissingFile(string value) => IsFile(value) && !File.Exists(value[1..]);

    private bool IsFile(string value) =>
        _kind == KeyValueValueKind.TextOrFile && value.StartsWith(KeyValuePairDialog.FileMarker);

    private Command Action(string label, Action<char?> execute, Func<bool> available) => new()
    {
        Id = $"KeyValueField.{Label}.{label}",
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get($"KeyValueField.{label}"),
        Importance = CommandImportance.Primary,
        Presentation = CommandPresentation.CommandBar,
        IsVisible = _ => available(),
        CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => execute(TuiKeybindHelpers.Echo($"KeyValueField.{label}"))
    };
}
