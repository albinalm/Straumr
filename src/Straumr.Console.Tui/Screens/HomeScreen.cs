using Straumr.Console.Tui.Components.Branding;
using Straumr.Console.Tui.Components.Panels;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens;

public class HomeScreen : Screen
{
    public HomeScreen(IReadOnlyList<string> workspaceLines)
    {
        Add(new Banner());

        Add(new ListPanel
        {
            Title = "Workspaces",
            X = 1,
            Y = Banner.FigletHeight + 1,
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
