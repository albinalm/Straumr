using Straumr.Console.Tui.Screens.Workspace;
using Straumr.Console.Tui.Screens.Request;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Services.Interfaces;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class StraumrTuiApp
{
    private const string OpenPromptCommandId = "Straumr.OpenCommandPrompt";

    private static readonly TimeSpan MessageLifetime = TimeSpan.FromSeconds(5);
    private static readonly Thickness FooterInset = new(1, 0, 1, 0);

    private readonly State<TuiScreen> _currentScreen;
    private readonly State<string?> _activeWorkspaceName = new(null);
    private readonly State<TuiCommandResult> _message = new(TuiCommandResult.None);
    private readonly Dictionary<TuiScreen, ITuiScreen> _screens;
    private ITuiScreen _screen;
    private TuiScreen? _pendingScreen;
    private bool _focusAfterNavigation;
    private readonly TuiCommandSet _commands = new();
    private readonly CommandPrompt _prompt;
    private readonly Queue<string> _submitted = new();
    private DateTimeOffset _messageExpiry;
    private bool _initialized;
    private TerminalApp? _app;
    private TuiExternalAction? _pendingExternalAction;
    private Visual? _focusOnAttach;

    /// <summary>
    /// Cancelled by the Ctrl+C global command. Linked into every <see cref="UpdateAsync"/> call so
    /// Ctrl+C can interrupt an in-flight load or operation the moment it is pressed, not merely once
    /// the current one happens to finish.
    /// </summary>
    private readonly CancellationTokenSource _interruptSource = new();

    public StraumrTuiApp(
        WorkspaceScreen workspaceScreen,
        RequestScreen requestScreen,
        IStraumrOptionsService optionsService)
    {
        _screens = new ITuiScreen[] { workspaceScreen, requestScreen }.ToDictionary(screen => screen.Kind);
        TuiScreen initialScreen = optionsService.Options.CurrentWorkspace is null
            ? TuiScreen.Workspaces
            : TuiScreen.Requests;
        _currentScreen = new State<TuiScreen>(initialScreen);
        _screen = _screens[initialScreen];
        foreach (ITuiScreen screen in _screens.Values)
        {
            screen.NotificationRequested += Notify;
            screen.ExternalActionRequested += RequestExternalAction;
            screen.Root.IsVisible = ReferenceEquals(screen, _screen);
        }
        SetCommands();

        _prompt = new CommandPrompt(_commands.Complete, _submitted.Enqueue);

        var commandBar = new CommandBar();
        commandBar.SetStyle(StraumrStyles.CommandBar);

        var footer = new ZStack(
                StraumrSurfaces.Inset(commandBar, FooterInset)
                    .IsVisible(() => !_prompt.IsOpen && _message.Value.Message is null),
                StraumrSurfaces.Inset(BuildMessageLine(), FooterInset)
                    .IsVisible(() => !_prompt.IsOpen && _message.Value.Message is not null),
                _prompt.Root)
            .HorizontalAlignment(Align.Stretch);

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
            .Cell(new ZStack(_screens.Values.Select(screen => screen.Root).ToArray())
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch), 2, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 3, 0)
            .Cell(footer, 4, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        var window = new Group()
            .Padding(new Thickness(0))
            .Content(shell)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
        window.SetStyle(StraumrStyles.WindowGroup);

        // An explicit window layer remains parentless after a run, allowing the retained tree to be
        // attached to a fresh TerminalApp after an external editor releases the terminal.
        Root = new WindowLayer(window);
    }

    public Visual Root { get; }

    public bool ExitRequested { get; private set; }

    public bool HasPendingExternalAction => _pendingExternalAction is not null;

    public async Task UpdateAsync(TerminalApp app, CancellationToken cancellationToken)
    {
        AttachTo(app);

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _interruptSource.Token);
        CancellationToken token = linked.Token;

        if (!_initialized)
        {
            _initialized = true;
            await _screen.LoadAsync(token);
            SetActiveWorkspace(_screen.ActiveWorkspaceName);
            return;
        }

        if (HasPendingExternalAction)
            return;

        _prompt.CloseIfFocusLost();
        ExpireMessage();
        await RunSubmittedCommandsAsync(token);
        if (ExitRequested)
            return;
        await NavigatePendingAsync(token);
        await _screen.UpdateAsync(token);
        SetActiveWorkspace(_screen.ActiveWorkspaceName);
        if (_focusAfterNavigation && _screen.FocusTarget.App == app)
        {
            _focusAfterNavigation = false;
            app.Focus(_screen.FocusTarget);
        }
    }

    private async Task NavigatePendingAsync(CancellationToken cancellationToken)
    {
        if (_pendingScreen is not { } kind)
            return;
        _pendingScreen = null;
        if (_screen.Kind == kind)
            return;
        _screen.Root.IsVisible = false;
        _screen = _screens[kind];
        _screen.Root.IsVisible = true;
        _currentScreen.Value = kind;
        _message.Value = TuiCommandResult.None;
        SetCommands();
        await _screen.LoadAsync(cancellationToken);
        _focusAfterNavigation = true;
    }

    private void SetCommands()
    {
        _commands.Clear();
        _commands.Add(new TuiCommand("quit", QuitAsync) { Aliases = ["q", "exit"] });
        _commands.Add(new TuiCommand("requests", (argument, _) => QueueNavigation(TuiScreen.Requests, argument)) { Aliases = ["rq"] });
        _commands.Add(new TuiCommand("workspaces", (argument, _) => QueueNavigation(TuiScreen.Workspaces, argument)) { Aliases = ["ws"] });
        foreach (TuiCommand command in _screen.PromptCommands)
            _commands.Add(command);
    }

    private Task<TuiCommandResult> QueueNavigation(TuiScreen screen, string argument)
    {
        if (argument.Length > 0)
            return Task.FromResult(TuiCommandResult.Failed($"usage: {screen.ToString().ToLowerInvariant()}"));
        _pendingScreen = screen;
        return Task.FromResult(TuiCommandResult.None);
    }

    public void SetActiveWorkspace(string? name) =>
        _activeWorkspaceName.Value = name;

    public void RequestExit() => ExitRequested = true;

    /// <remarks>
    /// Ctrl+C is the terminal's universal "stop this" convention, distinct from the deliberate
    /// <c>:q</c> vocabulary the rest of the app uses to exit. Leaving it unclaimed meant the
    /// keystroke simply vanished: nothing exited, nothing cancelled, no feedback at all. It is a
    /// global command rather than routed through a screen so it still works with a dialog focused,
    /// which is exactly when a user reaches for it.
    /// </remarks>
    private Command BuildInterruptCommand() =>
        new()
        {
            Id = "Straumr.Interrupt",
            LabelMarkup = "Cancel",
            Gesture = new KeyGesture((char)('C' & 0x1F), TerminalModifiers.Ctrl),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.None,
            Execute = _ => RequestInterrupt()
        };

    private void RequestInterrupt()
    {
        _interruptSource.Cancel();
        RequestExit();
    }

    public async Task RunPendingExternalActionAsync(CancellationToken cancellationToken)
    {
        TuiExternalAction? action = _pendingExternalAction;
        _pendingExternalAction = null;
        if (action is null)
            return;

        Notify(await action.ExecuteAsync(cancellationToken));
        _focusOnAttach = action.FocusTarget;
    }

    private void AttachTo(TerminalApp app)
    {
        if (ReferenceEquals(_app, app))
            return;

        _app = app;
        app.RemoveGlobalCommand(TerminalApp.DefaultQuitCommandId);
        foreach (Command command in BuildOpenPromptCommands())
            app.AddGlobalCommand(command);
        app.AddGlobalCommand(BuildInterruptCommand());

        if (_focusOnAttach is not { } focusTarget)
            return;

        _focusOnAttach = null;
        app.Focus(focusTarget);
    }

    private void RequestExternalAction(TuiExternalAction action)
    {
        _pendingExternalAction ??= action;
    }

    private Visual BuildMessageLine() =>
        new TextBlock(() => _message.Value.Message ?? string.Empty)
            .Style(() => _message.Value.IsError
                ? StraumrStyles.RedText
                : StraumrStyles.MutedText)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    private IEnumerable<Command> BuildOpenPromptCommands()
    {
        yield return BuildOpenPromptCommand(
            OpenPromptCommandId,
            TerminalModifiers.None,
            CommandPresentation.CommandBar);
        yield return BuildOpenPromptCommand(
            $"{OpenPromptCommandId}.Shifted",
            TerminalModifiers.Shift,
            CommandPresentation.None);
    }

    private Command BuildOpenPromptCommand(
        string id,
        TerminalModifiers modifiers,
        CommandPresentation presentation) =>
        new()
        {
            Id = id,
            LabelMarkup = "Command",
            Gesture = new KeyGesture(':', modifiers),
            Importance = CommandImportance.Secondary,
            Presentation = presentation,
            CanExecute = _ => !_prompt.IsOpen && !IsModalOpen,
            IsVisible = _ => !IsModalOpen,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => OpenPrompt()
        };

    /// <summary>
    /// Whether a modal surface such as a dialog owns the keyboard. The prompt belongs to the shell
    /// and its commands operate on the screen behind a dialog, so while one is up the gesture is
    /// neither offered nor allowed.
    /// </summary>
    /// <remarks>
    /// A global command is collected alongside the focus chain's rather than from it, so modality
    /// does not suppress it the way it suppresses a gesture on a visual: without this the folder
    /// browser advertised <c>: Command</c> among its own navigation keys.
    /// </remarks>
    private bool IsModalOpen
    {
        get
        {
            for (Visual? visual = _app?.FocusedElement; visual is not null; visual = visual.Parent)
            {
                if (visual is IModalVisual { IsModal: true } && !ReferenceEquals(visual, _prompt.Root))
                    return true;
            }

            return false;
        }
    }

    private void OpenPrompt()
    {
        _message.Value = TuiCommandResult.None;
        _prompt.Open();
    }

    private async Task RunSubmittedCommandsAsync(CancellationToken cancellationToken)
    {
        while (_submitted.Count > 0)
            Notify(await _commands.ExecuteAsync(_submitted.Dequeue(), cancellationToken));
    }

    private void Notify(TuiCommandResult result)
    {
        _message.Value = result;
        if (result.Message is not null)
            _messageExpiry = DateTimeOffset.UtcNow + MessageLifetime;
    }

    private void ExpireMessage()
    {
        if (_message.Value.Message is not null && DateTimeOffset.UtcNow >= _messageExpiry)
            _message.Value = TuiCommandResult.None;
    }

    private Task<TuiCommandResult> QuitAsync(string argument, CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
            return Task.FromResult(TuiCommandResult.Failed("usage: quit"));

        RequestExit();
        return Task.FromResult(TuiCommandResult.None);
    }
}
