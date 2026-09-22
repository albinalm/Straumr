using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal sealed class MessageField : EditorField
{
    public MessageField(string label, string message) : base(label)
    {
        GrowsToFill = true;
        Content = ResourceScreenLayoutHelpers.Message(
            new TextBlock(message)
                .Style(StraumrStyleService.MutedText)
                .Wrap(true)
                .Trimming(TextTrimming.EndEllipsis));
    }

    public override Visual Content { get; }

    public override Visual FocusTarget => Content;
}
