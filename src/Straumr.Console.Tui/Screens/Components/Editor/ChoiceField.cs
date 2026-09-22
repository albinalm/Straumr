using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class ChoiceField<T> : EditorField
{
    private readonly Select<string> _select;
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
        _values = values;
        _set = set;
        int index = Math.Max(0, values.ToList().FindIndex(value => EqualityComparer<T>.Default.Equals(value, initial)));
        _select = new Select<string>(labels, index)
        {
            HorizontalAlignment = Align.Stretch
        };
        _select.SetStyle(StraumrStyleService.Select);
        _select.WithMovementKeys();
        _select.IsTabStop(_select.IsReachable);
        _select.SelectionChanged(() =>
        {
            _set(Value);
            Changed?.Invoke();
        });
        Content = _select;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _select;

    public T Value => _values[Math.Clamp(_select.SelectedIndex, 0, _values.Count - 1)];
}
