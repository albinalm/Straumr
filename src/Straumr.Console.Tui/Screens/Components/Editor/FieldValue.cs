using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Layout;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class FieldValue : Visual
{
    private readonly Action<char?> _edit;
    private readonly TextBlock _text;

    public FieldValue(Func<string> text, Func<bool> muted, Action<char?> edit, string action = "Edit")
    {
        _text = new TextBlock(text)
            .Style(() => muted() ? StraumrStyleService.MutedText : StraumrStyleService.PrimaryText)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);
        _edit = edit;
        AttachChild(_text);
        Focusable = true;
        this.IsTabStop(this.IsReachable);
        HorizontalAlignment = Align.Stretch;
        AddCommand(EditCommand("EditorField.Activate", action, CommandPresentation.CommandBar, edit));
        AddCommand(EditCommand("EditorField.ActivateAlternate", action, CommandPresentation.None, edit));
        AddCommand(EditCommand("EditorField.Edit", action, TuiKeybindHelpers.SecondaryPresentation("EditorField.Activate"), edit));
    }

    protected override int ChildrenCount => 1;

    protected override Visual GetChild(int index) =>
        index == 0 ? _text : throw new ArgumentOutOfRangeException(nameof(index));

    protected override SizeHints MeasureCore(in LayoutConstraints constraints) => _text.Measure(constraints);

    protected override void ArrangeCore(in Rectangle finalRect) => _text.Arrange(finalRect);

    protected override void OnPointerPressed(PointerEventArgs e)
    {
        if (e.Button != TerminalMouseButton.Left)
        {
            return;
        }

        App?.Focus(this);
        _edit(null);
        e.Handled = true;
    }

    private static Command EditCommand(string id, string label, CommandPresentation presentation, Action<char?> edit) => new()
    {
        Id = id,
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get(id),
        Importance = CommandImportance.Primary,
        Presentation = presentation,
        Execute = _ => edit(TuiKeybindHelpers.Echo(id))
    };
}
