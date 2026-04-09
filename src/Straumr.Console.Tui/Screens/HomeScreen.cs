using Straumr.Console.Tui.Components;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens;

public class HomeScreen : Screen
{
    public HomeScreen(IReadOnlyList<string> workspaceLines)
    {
        Add(new Banner
        {
            X = Pos.AnchorEnd(Branding.FigletWidth + 1),
            Y = 0,
        });

        Add(new ListPanel
        {
            Title = "Workspaces",
            X = 1,
            Y = Branding.FigletHeight + 1,
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
