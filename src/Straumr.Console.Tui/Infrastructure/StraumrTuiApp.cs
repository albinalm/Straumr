using Straumr.Console.Tui.Screens.Workspace;
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
    private readonly WorkspaceScreen _workspaceScreen;
    private bool _initialized;

    public StraumrTuiApp(WorkspaceScreen workspaceScreen)
    {
        _workspaceScreen = workspaceScreen;
        _screenContent = new State<Visual>(workspaceScreen.Root);

        var commandBar = new CommandBar();
        commandBar.SetStyle(StraumrStyles.CommandBar);

        var shell = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(StraumrHeader.Create(_currentScreen, _activeWorkspaceName), 0, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 1, 0)
            .Cell(new ComputedVisual(() => _screenContent.Value)
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch), 2, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 3, 0)
            .Cell(StraumrSurfaces.Inset(commandBar, new Thickness(1, 0, 1, 0)), 4, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        var window = new Group()
            .Padding(new Thickness(0))
            .Content(shell)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
        window.SetStyle(StraumrStyles.WindowGroup);

        Root = window;
    }

    public Visual Root { get; }

    public bool ExitRequested { get; private set; }

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            await _workspaceScreen.UpdateAsync(cancellationToken);
            return;
        }

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
