using Straumr.Console.Tui.Components.Prompts.Base;
using Straumr.Console.Tui.Helpers;
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
            Height = Dim.Fill(3),
        };
        detailsView.Initialized += (_, _) => detailsView.SetFocus();

        frame.Add(detailsView);
        return frame;
    }
}
