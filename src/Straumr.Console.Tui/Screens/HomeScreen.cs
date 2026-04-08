using Straumr.Console.Tui.Components;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens;

public class HomeScreen : Screen
{
    private const string Figlet = """
                                       _
                                   ___| |_ _ __ __ _ _   _ _ __ ___  _ __
                                  / __| __| '__/ _` | | | | '_ ` _ \| '__|
                                  \__ \ |_| | | (_| | |_| | | | | | | |
                                  |___/\__|_|  \__,_|\__,_|_| |_| |_|_|

                                  """;

    public HomeScreen(IReadOnlyList<string> workspaceLines)
    {
        int figletWidth = Figlet.Split('\n').Max(l => l.Length);
        int figletHeight = Figlet.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        Add(new Banner
        {
            Text = Figlet,
            X = Pos.AnchorEnd(figletWidth + 1),
            Y = 0,
        });

        Add(new ListPanel
        {
            Title = "Workspaces",
            X = 1,
            Y = figletHeight + 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(1),
            Items = workspaceLines,
        });
    }

    public override bool OnKeyDown(Key key)
    {
        if (key == Key.Esc)
        {
            Quit();
            return true;
        }

        return false;
    }
}
