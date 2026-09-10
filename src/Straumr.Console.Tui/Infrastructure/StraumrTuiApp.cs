using Straumr.Console.Tui.Screens.Workspace;
using Straumr.Console.Tui.Visuals.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class StraumrTuiApp
{
    private readonly State<TuiScreen> _currentScreen = new(TuiScreen.Workspaces);
    private readonly State<string?> _activeWorkspaceName = new(null);
    private readonly State<Visual> _screenContent;
    private readonly WorkspaceScreen _workspaceScreen;
    private bool _initialized;

    public StraumrTuiApp(WorkspaceScreen workspaceScreen)
    {
        _workspaceScreen = workspaceScreen;
        _screenContent = new State<Visual>(workspaceScreen.Root);

        Root = new DockLayout()
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch)
            .Top(StraumrHeader.Create(_currentScreen, _activeWorkspaceName))
            .Content(() => _screenContent.Value)
            .Bottom(new CommandBar());
    }

    public Visual Root { get; }

    public bool ExitRequested { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        _initialized = true;
        await _workspaceScreen.LoadAsync(cancellationToken);
        SetActiveWorkspace(_workspaceScreen.ActiveWorkspaceName);
    }

    public void ShowScreen(TuiScreen screen, Visual content)
    {
        _currentScreen.Value = screen;
        _screenContent.Value = content;
    }

    public void SetActiveWorkspace(string? name) =>
        _activeWorkspaceName.Value = name;

    public void RequestExit() => ExitRequested = true;
}
