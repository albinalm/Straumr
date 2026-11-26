using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class ChoiceField<T> : EditorField
{
    private readonly FieldValue _display;
    private readonly IReadOnlyList<string> _labels;
    private readonly State<int> _selected;
    private readonly Action<T> _set;
    private readonly IReadOnlyList<T> _values;

    public ChoiceField(
        string label,
        IReadOnlyList<string> labels,
        IReadOnlyList<T> values,
        T initial,
        Action<T> set)
        : base(label)
    {
        _labels = labels;
        _values = values;
        _set = set;
        _selected = new State<int>(Math.Max(0, values.ToList().FindIndex(value => EqualityComparer<T>.Default.Equals(value, initial))));
        _display = new FieldValue(() => _labels[_selected.Value], () => false, _ => Choose());
        Content = _display;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _display;

    public T Value => _values[Math.Clamp(_selected.Value, 0, _values.Count - 1)];

    private void Choose() => new ChoicePickerDialog(Label, _labels, _selected.Value, index =>
    {
        if (index == _selected.Value)
        {
            return;
        }

        _selected.Value = index;
        _set(Value);
        Changed?.Invoke();
    }).Show();
}
