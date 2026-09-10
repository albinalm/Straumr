using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class StraumrTuiApp
{
    private readonly State<TuiScreen> _currentScreen = new(TuiScreen.Workspaces);
    private readonly State<string?> _activeWorkspaceName = new(null);
    private readonly State<Visual> _screenContent;

    public StraumrTuiApp()
    {
        _screenContent = new State<Visual>(BuildWorkspacePlaceholder());

        Root = new DockLayout()
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch)
            .Top(StraumrHeader.Create(_currentScreen, _activeWorkspaceName))
            .Content(() => _screenContent.Value)
            .Bottom(new CommandBar());
    }

    public Visual Root { get; }

    public bool ExitRequested { get; private set; }

    public void ShowScreen(TuiScreen screen, Visual content)
    {
        _currentScreen.Value = screen;
        _screenContent.Value = content;
    }

    public void SetActiveWorkspace(string? name) =>
        _activeWorkspaceName.Value = name;

    public void RequestExit() => ExitRequested = true;

    private static Visual BuildWorkspacePlaceholder() =>
        new Group("Workspaces")
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch)
            .Padding(new Thickness(1))
            .Content(new Center(
                new TextBlock("Workspace browser is not loaded.")));
}
