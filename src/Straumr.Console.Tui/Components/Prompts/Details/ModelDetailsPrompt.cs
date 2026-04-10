using System.Text;
using Straumr.Console.Tui.Components.Prompts.Base;
using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Drawing;
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

        int maxKeyWidth = Math.Clamp(Rows.Max(r => r.Key.Length), 8, 24);
        var textBuilder = new StringBuilder();
        foreach ((string Key, string Value) row in Rows)
        {
            textBuilder.Append(row.Key.PadRight(maxKeyWidth));
            textBuilder.Append("  ");
            textBuilder.Append(MarkupText.ToPlain(row.Value));
            textBuilder.AppendLine(" ");
        }

        InteractiveTextView detailsView = new()
        {
            Text = textBuilder.ToString().TrimEnd('\r', '\n'),
            ReadOnly = true,
            PreserveTrailingSpaces = true,
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(3),
        };
        ApplyTextTheme(detailsView);
        detailsView.Initialized += (_, _) => detailsView.SetFocus();

        frame.Add(detailsView);
        return frame;
    }

    private void ApplyTextTheme(InteractiveTextView text)
    {
        if (Theme is null)
        {
            return;
        }

        Color background = ColorResolver.Resolve(Theme.Surface);
        Color foreground = ColorResolver.Resolve(Theme.OnSurface);
        text.ApplyTheme(background, foreground);
    }
}
