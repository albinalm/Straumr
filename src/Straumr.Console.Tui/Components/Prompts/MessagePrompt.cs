using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts;

internal sealed class MessagePrompt : PromptComponent
{
    public required string Title { get; init; }
    public required string Message { get; init; }

    public override View Build()
    {
        FrameView frame = CreateFrame(Title);

        TextView text = new()
        {
            Text = MarkupText.ToPlain(Message),
            ReadOnly = true,
            WordWrap = true,
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
        };

        frame.Add(text);
        return frame;
    }
}
