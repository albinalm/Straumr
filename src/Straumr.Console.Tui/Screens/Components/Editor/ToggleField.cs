using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class ToggleField : EditorField
{
    private readonly Switch _switch;

    public ToggleField(string label, bool initial, Action<bool> set) : base(label)
    {
        _switch = new Switch { IsOn = initial };
        _switch.SetStyle(StraumrStyleService.Switch);
        _switch.IsTabStop(_switch.IsReachable);
        _switch.Toggled(() =>
        {
            set(_switch.IsOn);
            Changed?.Invoke();
        });
        Content = new HStack(_switch).HorizontalAlignment(Align.Start);
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => _switch;
}
