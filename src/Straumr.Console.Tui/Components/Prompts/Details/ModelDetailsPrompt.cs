using Straumr.Console.Tui.Components.Prompts.Base;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using MarkupText = Straumr.Console.Tui.Helpers.MarkupText;

namespace Straumr.Console.Tui.Components.Prompts.Details;

internal sealed class ModelDetailsPrompt : PromptComponent
{
    public required string Title { get; init; }
    public required IReadOnlyList<(string Key, string Value)> Rows { get; init; }
    public string EmptyMessage { get; init; } = "No details available";

    public override View Build()
    {
        FrameView frame = CreateFrame(Title);

        if (Rows.Count == 0)
        {
            Label empty = new()
            {
                Text = MarkupText.ToPlain(EmptyMessage),
                X = 1,
                Y = 1,
                Width = Dim.Fill(2),
            };

            frame.Add(empty);
            return frame;
        }

        SelectableDetailsView detailsView = new(Rows, Theme)
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Absolute(1),
        };
        detailsView.Initialized += (_, _) => detailsView.SetFocus();

        var sized = false;
        frame.DrawComplete += (_, _) =>
        {
            if (sized) return;
            sized = true;
            int lineCount = detailsView.ComputeLineCount(detailsView.Viewport.Width);
            detailsView.Height = Dim.Absolute(lineCount);
            frame.Height = Dim.Absolute(lineCount + 4); // 2 borders + 1 for Y=1 +1 for whitespace
        };

        frame.Add(detailsView);
        return frame;
    }
}
