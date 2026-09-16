using Straumr.Console.Tui.Screens.Auth;
using Straumr.Console.Tui.Screens.Workspace;
using Straumr.Console.Tui.Screens.Request;
using Straumr.Console.Tui.Screens.Secret;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Console.Tui.Visuals.Theming;
using Straumr.Core.Services.Interfaces;
using Tomlyn;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class StraumrTuiApp
{
    private const string OpenPromptCommandId = "Straumr.OpenCommandPrompt";

    private static readonly TimeSpan MessageLifetime = TimeSpan.FromSeconds(5);

    private readonly State<TuiScreen> _currentScreen;
    private readonly State<string?> _activeWorkspaceName = new(null);
    private readonly State<TuiCommandResult> _message = new(TuiCommandResult.None);
    private readonly Dictionary<TuiScreen, ITuiScreen> _screens;
    private ITuiScreen _screen;
    private PendingNavigation? _pendingNavigation;
    private bool _focusAfterNavigation;
    private readonly TuiCommandSet _commands = new();
    private readonly CommandPrompt _prompt;
    private readonly Queue<string> _submitted = new();
    private readonly Stack<TuiScreen> _returnScreens = new();
    private DateTimeOffset _messageExpiry;
    private bool _initialized;
    private bool _workspaceContextLoaded;
    private bool _transientOpened;
    private TerminalApp? _app;
    private TuiExternalAction? _pendingExternalAction;
    private Visual? _focusOnAttach;
    private TuiCommandResult? _pendingAnnouncement;

    /// <summary>
    /// Cancelled by the Ctrl+C global command. Linked into every <see cref="UpdateAsync"/> call so
    /// Ctrl+C can interrupt an in-flight load or operation the moment it is pressed, not merely once
    /// the current one happens to finish.
    /// </summary>
    private readonly CancellationTokenSource _interruptSource = new();

    private readonly IStraumrSettingsService _settingsService;
    private readonly ThemeSelection _themeSelection;
    private readonly ExternalEditor _editor;

    public StraumrTuiApp(
        WorkspaceScreen workspaceScreen,
        RequestScreen requestScreen,
        AuthScreen authScreen,
        SecretScreen secretScreen,
        IStraumrStateService stateService,
        IStraumrSettingsService settingsService,
        ThemeSelection themeSelection,
        ExternalEditor editor)
    {
        _settingsService = settingsService;
        _themeSelection = themeSelection;
        _editor = editor;
        _screens = new ITuiScreen[] { workspaceScreen, requestScreen, authScreen, secretScreen }
            .ToDictionary(screen => screen.Kind);
        TuiScreen initialScreen = stateService.State.CurrentWorkspace is null
            ? TuiScreen.Workspaces
            : TuiScreen.Requests;
        _currentScreen = new State<TuiScreen>(initialScreen);
        _screen = _screens[initialScreen];
        foreach (ITuiScreen screen in _screens.Values)
        {
            screen.NotificationRequested += Notify;
            screen.ExternalActionRequested += RequestExternalAction;
            screen.TransientScreenOpened += () => _transientOpened = true;
            screen.TransientScreenClosed += ReturnFromTransientScreen;
            SetOnShow(screen, ReferenceEquals(screen, _screen));
        }
        SetCommands();

        _prompt = new CommandPrompt(_commands.Complete, _submitted.Enqueue);

        // Wrapped rather than clipped. The row is as wide as the terminal and the hints are as many
        // as the focused region has, so a bar held to one row silently drops the last of them — and
        // the ones it drops are at the end, which is where the less-used actions sit and where a
        // reader looks when they do not already know the key. The row grows instead; the screen
        // above it gives up the lines, which is the right way round for a row that is only as tall
        // as it has to be.
        var commandBar = new CommandBar { MultiLine = true };
        commandBar.SetStyle(StraumrStyles.CommandBar);

        var footer = new ZStack(
                StraumrSurfaces.Inset(commandBar, StraumrSurfaces.RowInset)
                    .IsVisible(() => !_prompt.IsOpen && _message.Value.Message is null),
                StraumrSurfaces.Inset(BuildMessageLine(), StraumrSurfaces.RowInset)
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

    /// <summary>
    /// Set when the applied palette changed under this shell. The host answers it by building a new
    /// shell, because a control cannot be handed a style twice: every visual in the tree was given
    /// its colours when it was constructed. See <see cref="Visuals.Shared.StraumrStyleSet"/>.
    /// </summary>
    public bool RestartRequested { get; private set; }

    public TuiScreen CurrentScreen => _currentScreen.Value;

    /// <summary>What the footer is currently saying, for a rebuild to carry across.</summary>
    public TuiCommandResult Message => _message.Value;

    /// <summary>
    /// Opens a rebuilt shell on the screen its predecessor was on and still saying what it was
    /// saying, so changing the theme neither moves the reader nor swallows the answer to what they
    /// just typed. The command that caused the rebuild reported into a footer that is being thrown
    /// away, which is the one thing a rebuild cannot simply inherit.
    /// </summary>
    /// <remarks>
    /// The screen is queued as a navigation so it takes the path every other screen change takes,
    /// including the load and the focus that follow it.
    /// </remarks>
    /// <summary>
    /// Says something on the footer as soon as the shell has a footer to say it on. Used for what
    /// the host learned before this existed — a settings file that would not parse, a theme that
    /// would not resolve, a state file carrying a setting that has moved.
    /// </summary>
    public void Announce(TuiCommandResult message)
    {
        if (message.Message is not null)
            _pendingAnnouncement = message;
    }

    public void ResumeOn(TuiScreen screen, TuiCommandResult message)
    {
        if (screen != _screen.Kind)
            _pendingNavigation = new PendingNavigation(screen, string.Empty, false, null);

        // Held rather than said now: a queued navigation clears the footer when it runs, which is
        // after this returns.
        _pendingAnnouncement = message.Message is null ? null : message;
    }

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
            AnnouncePending();
            await SetActiveWorkspaceAsync(_screen.ActiveWorkspaceName, token);
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
        AnnouncePending();
        await _screen.UpdateAsync(token);
        await SetActiveWorkspaceAsync(_screen.ActiveWorkspaceName, token);
        if (_focusAfterNavigation && !IsModalOpen && !IsModalShown(app) && _screen.FocusTarget.App == app)
        {
            _focusAfterNavigation = false;
            app.Focus(_screen.FocusTarget);
        }

        RestoreStrayFocus(app);
    }

    /// <summary>
    /// Shows or hides a screen, and with it the claim its main region makes on stray focus.
    /// </summary>
    /// <remarks>
    /// Both are assigned outright rather than bound. <c>AutoFocus</c> was a binding over
    /// <see cref="FocusScope.IsReachable"/> on each screen's list, which answers the right question
    /// only while the list is in the tree: every reload swaps it out for the loading message, and a
    /// binding evaluated on a detached visual walks no ancestors, so it answers <see langword="true"/>
    /// and registers nothing that could ever invalidate it again. A hidden screen's list then stayed
    /// a standing claim on every stray focus in the app — which is how deleting a secret left the
    /// reader on Secrets with the Workspaces list focused, no region titled and Workspaces' keys in
    /// the footer. Which screen is on show is the shell's own fact and needs no binding to track it.
    /// </remarks>
    private static void SetOnShow(ITuiScreen screen, bool onShow)
    {
        screen.Root.IsVisible = onShow;
        screen.FocusTarget.AutoFocus = onShow;
    }

    /// <summary>
    /// Holds the invariant that focus belongs to the screen the reader is looking at, unless a modal
    /// or the command prompt has taken it.
    /// </summary>
    /// <remarks>
    /// Focus is lost whenever what holds it leaves the tree, and an operation run from a dialog
    /// loses it twice: once when the dialog closes, and again when the reload that follows detaches
    /// the list behind it. Left to <c>AutoFocus</c> alone there is a window with no claimant at all,
    /// and focus that lands on a visual which is then stranded is never re-homed, because the
    /// framework only re-homes focus it has seen go null. Rather than have every screen re-focus
    /// itself after every operation — which each delete, save and refresh would have to remember —
    /// the shell restores it once, here, for all of them.
    /// </remarks>
    private void RestoreStrayFocus(TerminalApp app)
    {
        // A detached focus target is a screen still loading; it is focusable again on the pass its
        // list comes back, and this runs on every pass.
        if (_prompt.IsOpen || IsModalOpen || _screen.FocusTarget.App != app)
            return;

        for (Visual? node = app.FocusedElement; node is not null; node = node.Parent)
            if (ReferenceEquals(node, _screen.Root))
                return;

        // Asked last because it walks the whole tree, and only a pass on which focus is already
        // astray pays for it. It is still owed: a dialog shown this pass holds no focus yet, so the
        // focus chain cannot see it, and taking focus to the screen behind it would strand it.
        if (!IsModalShown(app))
            app.Focus(_screen.FocusTarget);
    }

    private async Task NavigatePendingAsync(CancellationToken cancellationToken)
    {
        if (_pendingNavigation is not { } pending)
            return;
        _pendingNavigation = null;

        if (pending.RunInPlace)
        {
            ITuiScreen target = _screens[pending.Screen];
            await target.LoadAsync(cancellationToken);
            TuiCommandResult result = await CreateScreenCommandSet(target)
                .ExecuteAsync(pending.Command, cancellationToken);
            if (!result.IsError)
                await _screen.LoadAsync(cancellationToken);
            Notify(result);
            return;
        }

        if (_screen.Kind != pending.Screen)
        {
            SetOnShow(_screen, false);
            _screen = _screens[pending.Screen];
            SetOnShow(_screen, true);
            _currentScreen.Value = pending.Screen;
            _message.Value = TuiCommandResult.None;
            SetCommands();
            await _screen.LoadAsync(cancellationToken);
            _focusAfterNavigation = true;
        }

        if (pending.Command.Length == 0)
            return;

        // The return is owed by what the command actually did, not by what it was expected to do:
        // `edit` opens the full-screen form for a request that reads and the external editor for
        // one that does not, and a create refused for want of a workspace opens nothing at all. The
        // screen says so by raising TransientScreenOpened, so every push here has a close to pop it.
        _transientOpened = false;
        TuiCommandResult dispatched = await CreateScreenCommandSet(_screen)
            .ExecuteAsync(pending.Command, cancellationToken);
        if (_transientOpened && pending.ReturnScreen is { } returnScreen)
            _returnScreens.Push(returnScreen);
        Notify(dispatched);
    }

    private void SetCommands()
    {
        _commands.Clear();
        _commands.Add(new TuiCommand("quit", QuitAsync) { Aliases = ["q", "exit"] });
        _commands.Add(NavigationCommand(TuiScreen.Requests, "request", "rq"));
        _commands.Add(NavigationCommand(TuiScreen.Workspaces, "workspace", "ws"));
        _commands.Add(NavigationCommand(TuiScreen.Auths, "auth", "au"));
        _commands.Add(NavigationCommand(TuiScreen.Secrets, "secret", "sc"));
        _commands.Add(new TuiCommand("settings", (argument, _) => OpenSettings(argument))
        {
            Aliases = ["set"],
            AllowPrefixMatch = false
        });
        _commands.Add(new TuiCommand("theme", ThemeAsync)
        {
            AllowPrefixMatch = false,
            CompleteArgument = CompleteTheme
        });
        foreach (TuiCommand command in _screen.PromptCommands)
            _commands.Add(command);
    }

    /// <summary>
    /// Hands the settings file to the reader's own editor, and reads it back when they are done.
    /// </summary>
    /// <remarks>
    /// The file itself is opened rather than a copy, because it is the reader's file: it lives at a
    /// path they know, they may have it open already, and their editor's own history for it should
    /// be the history of the file and not of a succession of temporary ones. Nothing is written back
    /// here — the editor already saved it — so a file left unparsable is simply reported and the
    /// previous settings stand until it is fixed.
    /// </remarks>
    private Task<TuiCommandResult> OpenSettings(string argument)
    {
        if (argument.Length > 0)
            return Task.FromResult(TuiCommandResult.Failed("usage: settings"));
        if (!_editor.IsConfigured)
            return Task.FromResult(TuiCommandResult.Failed("settings: no default editor is configured"));

        RequestExternalAction(new TuiExternalAction(EditSettingsAsync, _screen.FocusTarget));
        return Task.FromResult(TuiCommandResult.None);
    }

    private async Task<TuiCommandResult> EditSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            string path = await _settingsService.EnsureFileAsync(cancellationToken);
            await _editor.EditFileAsync(path, cancellationToken);
        }
        catch (ExternalEditorException exception)
        {
            return TuiCommandResult.Failed($"settings: {exception.Message}");
        }

        await _settingsService.LoadAsync(cancellationToken);
        bool changed = _themeSelection.Apply();
        RestartRequested = changed;

        if (_themeSelection.Message is { } problem)
            return TuiCommandResult.Failed(problem);

        return changed
            ? TuiCommandResult.Ok($"theme {StraumrStyles.ThemeName}")
            : TuiCommandResult.None;
    }

    /// <summary>
    /// Reports the theme in force, and writes a built-in out as a file to start a custom one from.
    /// Choosing a theme is the settings file's job; this is only what that job needs alongside it.
    /// </summary>
    /// <summary>
    /// Reports the theme, changes it, or writes a built-in out as a file to start one from.
    /// </summary>
    /// <remarks>
    /// Changing it writes the one key into the settings file through the TOML syntax tree, so the
    /// reader's comments survive, and then rebuilds the shell the same way saving the file by hand
    /// does. Naming the file in the reply would be telling someone who just typed a command where
    /// the command wrote; that belongs in documentation, not in a footer that expires in seconds.
    /// </remarks>
    private async Task<TuiCommandResult> ThemeAsync(string argument, CancellationToken cancellationToken)
    {
        string text = argument.Trim();
        if (text.Length == 0)
            return TuiCommandResult.Ok($"theme {StraumrStyles.ThemeName}");

        string[] parts = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["export", var name, ..])
            return Export(name, parts.Length > 2 ? parts[2] : null);

        if (!TuiCommandArguments.TryParseSingle(text, out string reference, out string? problem))
            return TuiCommandResult.Failed($"theme: {problem}");

        if (!StraumrThemes.TryResolve(reference, _settingsService.SettingsDirectory, out _, out string? error))
            return TuiCommandResult.Failed(error!);

        try
        {
            await _settingsService.SetThemeAsync(reference, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TomlException)
        {
            return TuiCommandResult.Failed($"theme not saved: {FirstLine(exception.Message)}");
        }

        if (!_themeSelection.Apply())
            return TuiCommandResult.Ok($"theme {StraumrStyles.ThemeName}");

        RestartRequested = true;
        return _themeSelection.Message is { } notice
            ? TuiCommandResult.Failed(notice)
            : TuiCommandResult.Ok($"theme {StraumrStyles.ThemeName}");
    }

    private TuiCommandResult Export(string name, string? path)
    {
        if (StraumrThemes.BuiltInSource(name) is not { } source)
        {
            return TuiCommandResult.Failed(
                $"no built-in theme {name}; there is {string.Join(", ", StraumrThemes.BuiltInNames)}");
        }

        string target = path ?? Path.Combine(
            _settingsService.SettingsDirectory, "themes", $"{name.ToLowerInvariant()}.toml");

        try
        {
            if (File.Exists(target))
                return TuiCommandResult.Failed($"{PathFormatting.Display(target)} already exists");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, source);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return TuiCommandResult.Failed($"export failed: {exception.Message}");
        }

        return TuiCommandResult.Ok($"wrote {PathFormatting.Display(target)}");
    }

    /// <summary>The footer is one row, so a multi-line failure gets its first line.</summary>
    private static string FirstLine(string message)
    {
        int end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message : message[..end];
    }

    private static TuiCommandCompletion CompleteTheme(string argument, int caret)
    {
        string text = argument[..Math.Clamp(caret, 0, argument.Length)];
        return text.StartsWith("export ", StringComparison.Ordinal)
            ? TuiCommandSet.Match(StraumrThemes.BuiltInNames, text["export ".Length..], "export ".Length)
            : TuiCommandSet.Match([..StraumrThemes.BuiltInNames, "export"], text, 0);
    }

    private TuiCommand NavigationCommand(TuiScreen screen, string name, string shortAlias) =>
        new(name, (argument, _) => QueueNavigation(screen, argument))
        {
            Aliases = [shortAlias],
            AllowPrefixMatch = false,
            CompleteArgument = (argument, caret) => CreateScreenCommandSet(_screens[screen]).Complete(argument, caret)
        };

    private Task<TuiCommandResult> QueueNavigation(TuiScreen screen, string command)
    {
        bool fromElsewhere = _screen.Kind != screen;
        TuiCommand? destinationCommand = CreateScreenCommandSet(_screens[screen]).ResolveCommand(command);
        bool runInPlace = fromElsewhere && destinationCommand is { RunsInPlaceFromOtherScreens: true };
        _pendingNavigation = new PendingNavigation(screen, command, runInPlace,
            fromElsewhere ? _screen.Kind : null);
        return Task.FromResult(TuiCommandResult.None);
    }

    private void ReturnFromTransientScreen()
    {
        if (_returnScreens.TryPop(out TuiScreen screen))
            _pendingNavigation = new PendingNavigation(screen, string.Empty, false, null);
    }

    private static TuiCommandSet CreateScreenCommandSet(ITuiScreen screen)
    {
        var commands = new TuiCommandSet();
        foreach (TuiCommand command in screen.PromptCommands)
            commands.Add(command);
        return commands;
    }

    private async Task SetActiveWorkspaceAsync(string? name, CancellationToken cancellationToken)
    {
        if (_workspaceContextLoaded && _activeWorkspaceName.Value == name)
            return;

        _workspaceContextLoaded = true;
        _activeWorkspaceName.Value = name;

        // Completion is synchronous while the prompt is open. Whenever the shared workspace
        // context changes, preload every hidden screen so its namespace commands can complete
        // against current data before the user visits it. Prime on startup even without an active
        // workspace: global secrets are still available, and their names must complete there too.
        foreach (ITuiScreen screen in _screens.Values.Where(screen => !ReferenceEquals(screen, _screen)))
            await screen.LoadAsync(cancellationToken);
    }

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

    /// <summary>
    /// <c>Ctrl+Tab</c> steps focus backwards, beside the <c>Shift+Tab</c> the framework already
    /// handles. Global, so it means the same thing on every screen and inside every dialog.
    /// </summary>
    /// <remarks>
    /// Unpresented, for the reason <c>Ctrl+Enter</c> is: many terminals send <c>Ctrl</c> with
    /// <c>Tab</c> as a bare <c>Tab</c>, and several — Windows Terminal among them — keep the
    /// combination for their own tab switching and never pass it on. A key that works on some
    /// terminals should not be promised on all of them, and where it does not arrive the portable
    /// <c>Shift+Tab</c> is unaffected.
    /// </remarks>
    private static Command BuildFocusPreviousCommand(TerminalApp app) =>
        new()
        {
            Id = "Straumr.FocusPrevious",
            LabelMarkup = "Previous field",
            Gesture = new KeyGesture(TerminalKey.Tab, TerminalModifiers.Ctrl),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.None,
            Execute = _ => app.FocusPrevious()
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

        // On the running app's own root rather than on Straumr's tree, because it governs every
        // cell no Straumr style reaches — the ground behind the shell, and the layers dialogs and
        // popups are hosted in, which are siblings of this shell rather than children of it.
        app.Root.SetStyle(Theme.Key, StraumrStyles.FrameworkTheme);

        app.RemoveGlobalCommand(TerminalApp.DefaultQuitCommandId);
        foreach (Command command in BuildOpenPromptCommands())
            app.AddGlobalCommand(command);
        app.AddGlobalCommand(BuildInterruptCommand());
        app.AddGlobalCommand(BuildFocusPreviousCommand(app));

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

    /// <summary>
    /// Whether a modal surface is up anywhere, focused or not.
    /// </summary>
    /// <remarks>
    /// <see cref="IsModalOpen"/> asks the focus chain, which can only answer once focus has reached
    /// the modal. A dialog that takes its own focus does so inside <c>Show</c> and is seen there,
    /// but one that leaves it to <c>AutoFocus</c> — every confirm, form and browser in this app —
    /// is not focused until the render that follows. Both open inside the pass a navigated command
    /// runs in, and the focus this shell restores after navigating would land behind them.
    /// </remarks>
    private bool IsModalShown(TerminalApp app) =>
        app.Root.EnumerateVisualsDepthFirst().Any(visual =>
            visual is IModalVisual { IsModal: true } && !ReferenceEquals(visual, _prompt.Root));

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

    /// <summary>
    /// Says what the shell this one replaced was saying, once the navigation that would have
    /// cleared it has run.
    /// </summary>
    private void AnnouncePending()
    {
        if (_pendingAnnouncement is not { } announcement)
            return;

        _pendingAnnouncement = null;
        Notify(announcement);
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

    /// <param name="ReturnScreen">
    /// The screen the command was typed on, when that is not the screen it runs on. It is returned
    /// to only if the command opened a full-screen surface, whose close is what asks for it back.
    /// </param>
    private readonly record struct PendingNavigation(
        TuiScreen Screen,
        string Command,
        bool RunInPlace,
        TuiScreen? ReturnScreen);
}
