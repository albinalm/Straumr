using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class ToggleField : EditorField
{
    public ToggleField(string label, bool initial, Action<bool> set) : base(label)
    {
        var enabled = new State<bool>(initial);
        Content = new FieldValue(() => enabled.Value ? "On" : "Off", () => false, _ =>
        {
            enabled.Value = !enabled.Value;
            set(enabled.Value);
            Changed?.Invoke();
        }, "Toggle");
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => Content;
}
