using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class ActionField : EditorField
{
    private readonly FieldValue _display;

    public ActionField(string label, string text, string action, Action execute) : base(label)
    {
        _display = new FieldValue(() => text, () => true, _ => execute(), action);
        Content = _display;
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _display;
}
