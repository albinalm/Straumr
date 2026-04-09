using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts;

internal sealed class TablePrompt : PromptComponent
{
    public required string Title { get; init; }
    public required string Column1 { get; init; }
    public required string Column2 { get; init; }
    public required IReadOnlyList<(string Key, string Value)> Rows { get; init; }
    public required string EmptyMessage { get; init; }

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
        }
        else
        {
            int maxKey = Math.Max(Column1.Length, Rows.Max(r => r.Key.Length));
            maxKey = Math.Clamp(maxKey, 8, 40);

            string headerText = $"{Column1.PadRight(maxKey)}  {Column2}";
            Label header = new()
            {
                Text = headerText,
                X = 1,
                Y = 1,
                Width = Dim.Fill(2),
            };

            ObservableCollection<string> entries = new(
                Rows.Select(row => $"{row.Key.PadRight(maxKey)}  {row.Value}").ToList());

            ListView list = new()
            {
                X = 1,
                Y = 2,
                Width = Dim.Fill(2),
                Height = Dim.Fill(2),
            };
            list.SetSource(entries);

            frame.Add(header, list);
        }

        return frame;
    }
}
