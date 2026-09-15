using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared.Editor;

/// <summary>
/// A map of names to values, edited in place. Request headers and parameters, a custom auth's own
/// headers and parameters, and a form body's fields are all the same thing, so they are all this.
/// </summary>
/// <remarks>
/// It is a <see cref="ResourceList"/> and not a grid of its own: selection, hover, focus response,
/// scrolling, <c>j</c>/<c>k</c>/<c>g</c>/<c>G</c> and double-click activation already arrive with the
/// list, and a pair reads as a resource — its name on the first line, what it holds on the second,
/// amber when populated and inert when empty, exactly as every other list in the app reads.
/// </remarks>
internal sealed class KeyValueField : EditorField
{
    private readonly IDictionary<string, string> _items;
    private readonly ResourceList _list;
    private readonly State<int> _selectedIndex = new(-1);
    private readonly string _entryName;
    private readonly KeyValueValueKind _kind;
    private List<string> _keys = [];

    /// <param name="entryName">
    /// What one entry is called, singular and lowercase — "header", "parameter", "part". It names
    /// the dialogs and the empty state, so the component itself needs no vocabulary of its own.
    /// </param>
    /// <param name="kind">
    /// Whether an entry's value may be a file from disk. Only a multipart part may; a header that
    /// offered a file picker would be offering something the protocol has no meaning for.
    /// </param>
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

        _list = new ResourceList([], ResourceScreenLayout.Message(
                new TextBlock($"No {entryName}s. Press a to add one.")
                    .Style(StraumrStyles.MutedText)
                    .Wrap(true)
                    .Trimming(TextTrimming.EndEllipsis)),
            activateLabel: "Edit");
        _list.BindSelectedIndex(_selectedIndex);
        // Activation is Enter or a double-click, neither of which types anything, so the dialog it
        // opens has no echo to discard.
        _list.ItemActivated += _ => Edit(null);
        _list.AddCommand(Action("Add", 'a', Add, () => true));
        _list.AddCommand(Action("Edit", 'e', Edit, () => Selected is not null));
        _list.AddCommand(Action("Remove", 'd', _ => Remove(), () => Selected is not null));

        Refresh(null);
        Content = ResourceScreenLayout.Scrollable(_list);
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _list;

    /// <summary>
    /// Re-reads the map after something other than this field changed it. Choosing a body type
    /// writes the <c>Content-Type</c> header, and a Headers page that did not know would be showing
    /// a header list the request does not have.
    /// </summary>
    public void Reload() => Refresh(Selected);

    private string? Selected =>
        (uint)_selectedIndex.Value < (uint)_keys.Count ? _keys[_selectedIndex.Value] : null;

    /// <param name="echo">
    /// The key that opened the dialog, discarded by the field that takes its focus. A printable
    /// gesture arrives as a key event and an independent text event, so <c>a</c> would otherwise
    /// name the new entry after the key that asked for it.
    /// </param>
    private void Add(char? echo)
    {
        var dialog = new KeyValuePairDialog($"Add {_entryName}", "Name", "Value",
            string.Empty, string.Empty, "Add",
            key => Validate(key, replacing: null), (key, value) =>
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
            return;

        var dialog = new KeyValuePairDialog($"Edit {_entryName}", "Name", "Value", key, _items[key], "Save",
            candidate => Validate(candidate, replacing: key), (candidate, value) =>
            {
                // A renamed entry is a removal and an addition, and the dictionary is ordered by its
                // insertion, so removing first keeps a rename from leaving the old name behind.
                if (!string.Equals(candidate, key, StringComparison.Ordinal))
                    _items.Remove(key);
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
            return;

        int index = _keys.IndexOf(key);
        _items.Remove(key);
        // The row below the one removed takes its place, as it does in every list that removes a row
        // under a cursor; on the last row that is the row above.
        Refresh(null);
        _selectedIndex.Value = _keys.Count == 0 ? -1 : Math.Clamp(index, 0, _keys.Count - 1);
        Changed?.Invoke();
    }

    /// <remarks>
    /// The comparison is the dictionary's own: request headers are case-insensitive and parameters
    /// are not, so what counts as a duplicate is a question only the map being edited can answer.
    /// </remarks>
    private string? Validate(string key, string? replacing)
    {
        if (key.Length == 0)
            return "Name cannot be empty.";
        if (replacing is not null && KeysEqual(key, replacing))
            return null;
        return _items.ContainsKey(key) ? $"A {_entryName} named {key} already exists." : null;
    }

    private bool KeysEqual(string left, string right) =>
        _items is Dictionary<string, string> map
            ? map.Comparer.Equals(left, right)
            : string.Equals(left, right, StringComparison.Ordinal);

    private void Refresh(string? select)
    {
        _keys = _items.Keys.ToList();
        _list.SetRows(_keys.Select(key => new ResourceRow(
            key,
            Meta: Describe(_items[key]),
            HasContent: _items[key].Length > 0,
            IsBroken: IsMissingFile(_items[key]))));

        int index = select is null ? -1 : _keys.IndexOf(select);
        if (index >= 0)
            _selectedIndex.Value = index;
        else if (_selectedIndex.Value >= _keys.Count)
            _selectedIndex.Value = _keys.Count - 1;
        else if (_selectedIndex.Value < 0 && _keys.Count > 0)
            _selectedIndex.Value = 0;
    }

    /// <summary>
    /// What the second line of a row says. A file part is named by its file rather than by the path
    /// leading to it, which is the half that identifies it and the half a narrow row keeps.
    /// </summary>
    private string Describe(string value)
    {
        if (value.Length == 0)
            return "empty";
        if (!IsFile(value))
            return value;

        string path = value[1..];
        return File.Exists(path)
            ? $"file · {Path.GetFileName(path)}"
            : $"file missing · {Path.GetFileName(path)}";
    }

    /// <summary>
    /// A part naming a file that is no longer there reads red, as any resource that cannot be used
    /// does, because the send is where it would otherwise be discovered.
    /// </summary>
    private bool IsMissingFile(string value) => IsFile(value) && !File.Exists(value[1..]);

    private bool IsFile(string value) =>
        _kind == KeyValueValueKind.TextOrFile && value.StartsWith(KeyValuePairDialog.FileMarker);

    /// <remarks>
    /// The action is handed the letter that ran it, so an action opening something that takes typed
    /// input has the keystroke to discard without having to name its own gesture a second time.
    /// </remarks>
    private Command Action(string label, char key, Action<char?> execute, Func<bool> available) => new()
    {
        Id = $"KeyValueField.{Label}.{label}",
        LabelMarkup = label,
        Gesture = new KeyGesture(key),
        Importance = CommandImportance.Primary,
        Presentation = CommandPresentation.CommandBar,
        IsVisible = _ => available(),
        CanExecute = _ => available(),
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => execute(key)
    };
}
