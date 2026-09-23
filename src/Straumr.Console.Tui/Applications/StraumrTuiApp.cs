using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Configuration;
using Straumr.Core.Services.Interfaces;
using Tomlyn;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;
using HintBar = Straumr.Console.Tui.Screens.Components.Shared.HintBar;

namespace Straumr.Console.Tui.Applications;

public sealed class StraumrTuiApp
{
    private const string OpenPromptCommandId = "Straumr.OpenCommandPrompt";

    private static readonly TimeSpan MessageLifetime = TimeSpan.FromSeconds(5);

    private static readonly (TuiScreen Screen, string Name, string Alias)[] Navigations =
    [
        (TuiScreen.Requests, "request", "rq"),
        (TuiScreen.Workspaces, "workspace", "ws"),
        (TuiScreen.Auths, "auth", "au"),
        (TuiScreen.Variables, "variable", "vr"),
        (TuiScreen.Secrets, "secret", "sc")
    ];
    private readonly State<string?> _activeWorkspaceName = new(null);
    private readonly TuiCommandSetModel _commands = new();

    private readonly State<TuiScreen> _currentScreen;
    private readonly ExternalEditorService _editor;

    private readonly CancellationTokenSource _interruptSource = new();
    private readonly State<TuiCommandResultModel> _message = new(TuiCommandResultModel.None);
    private readonly CommandPrompt _prompt;
    private readonly QuickStartService _quickStart;
    private readonly QuickStartView? _quickStartView;
    private readonly Stack<TuiScreen> _returnScreens = new();
    private readonly Dictionary<TuiScreen, ITuiScreen> _screens;

    private readonly IStraumrSettingsService _settingsService;
    private readonly Queue<string> _submitted = new();
    private readonly ThemeSelectionService _themeSelection;
    private TerminalApp? _app;
    private bool _focusAfterNavigation;
    private Visual? _focusOnAttach;
    private bool _initialized;
    private DateTimeOffset _messageExpiry;
    private TuiCommandResultModel? _pendingAnnouncement;
    private TuiExternalActionModel? _pendingExternalAction;
    private TuiPendingNavigationModel? _pendingNavigation;
    private ITuiScreen _screen;
    private bool _transientOpened;
    private bool _workspaceContextLoaded;

    public StraumrTuiApp(
        WorkspaceScreen workspaceScreen,
        RequestScreen requestScreen,
        AuthScreen authScreen,
        VariableScreen variableScreen,
        SecretScreen secretScreen,
        IStraumrStateService stateService,
        IStraumrSettingsService settingsService,
        ThemeSelectionService themeSelection,
        ExternalEditorService editor,
        QuickStartService quickStart)
    {
        _quickStart = quickStart;
        _settingsService = settingsService;
        _themeSelection = themeSelection;
        _editor = editor;
        _screens = new ITuiScreen[] { workspaceScreen, requestScreen, authScreen, variableScreen, secretScreen }
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

        var commandBar = new HintBar();
        commandBar.SetStyle(StraumrStyleService.CommandBar);

        ZStack footer = new ZStack(
                StraumrSurfaceHelpers.Inset(commandBar, StraumrSurfaceHelpers.RowInset)
                    .IsVisible(() => !_prompt.IsOpen && _message.Value.Message is null),
                StraumrSurfaceHelpers.Inset(BuildMessageLine(), StraumrSurfaceHelpers.RowInset)
                    .IsVisible(() => !_prompt.IsOpen && _message.Value.Message is not null),
                _prompt.Root)
            .HorizontalAlignment(Align.Stretch);

        Grid shell = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(StraumrHeaderHelpers.Create(_currentScreen, _activeWorkspaceName, BuildScreenMenu), 0, 0)
            .Cell(StraumrSurfaceHelpers.HorizontalDivider(), 1, 0)
            .Cell(new ZStack(_screens.Values.Select(screen => screen.Root).ToArray())
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch), 2, 0)
            .Cell(StraumrSurfaceHelpers.HorizontalDivider(), 3, 0)
            .Cell(footer, 4, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        Group window = new Group()
            .Padding(new Thickness(0))
            .Content(shell)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
        window.SetStyle(StraumrStyleService.WindowGroup);

        Root = new WindowLayer(window);
        if (quickStart.Active)
        {
            foreach (ITuiScreen screen in _screens.Values)
            {
                SetOnShow(screen, false);
            }

            _quickStartView = new QuickStartView(quickStart, themeSelection);
            Root = new WindowLayer(_quickStartView.Root);
        }
    }

    public Visual Root { get; }

    public bool ExitRequested { get; private set; }

    public bool HasPendingExternalAction => _pendingExternalAction is not null;

    public bool RestartRequested { get; private set; }

    public TuiScreen CurrentScreen => _currentScreen.Value;

    public TuiCommandResultModel Message => _message.Value;

    private bool IsModalOpen
    {
        get
        {
            for (Visual? visual = _app?.FocusedElement; visual is not null; visual = visual.Parent)
            {
                if (visual is IModalVisual { IsModal: true } && !ReferenceEquals(visual, _prompt.Root))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Announce(TuiCommandResultModel message)
    {
        if (message.Message is not null)
        {
            _pendingAnnouncement = message;
        }
    }

    public void ResumeOn(TuiScreen screen, TuiCommandResultModel message)
    {
        if (screen != _screen.Kind)
        {
            _pendingNavigation = new TuiPendingNavigationModel(screen, string.Empty, false, null);
        }

        _pendingAnnouncement = message.Message is null ? null : message;
    }

    public async Task UpdateAsync(TerminalApp app, CancellationToken cancellationToken)
    {
        AttachTo(app);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _interruptSource.Token);
        CancellationToken token = linked.Token;

        if (_quickStartView is not null)
        {
            await _quickStartView.UpdateAsync(token);
            if (!_quickStart.Active)
            {
                _currentScreen.Value = TuiScreen.Requests;
                RestartRequested = true;
            }
            else if (_quickStartView.PreviewChanged)
            {
                RestartRequested = true;
            }

            return;
        }

        if (!_initialized)
        {
            _initialized = true;
            await _screen.LoadAsync(token);
            AnnouncePending();
            await SetActiveWorkspaceAsync(_screen.ActiveWorkspaceName, token);
            return;
        }

        if (HasPendingExternalAction)
        {
            return;
        }

        _prompt.CloseIfFocusLost();
        ExpireMessage();
        await RunSubmittedCommandsAsync(token);
        if (ExitRequested || RestartRequested)
        {
            return;
        }

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

    private static void SetOnShow(ITuiScreen screen, bool onShow)
    {
        screen.Root.IsVisible = onShow;
        screen.FocusTarget.AutoFocus = onShow;
    }

    private void RestoreStrayFocus(TerminalApp app)
    {
        if (_prompt.IsOpen || IsModalOpen || _screen.FocusTarget.App != app)
        {
            return;
        }

        for (Visual? node = app.FocusedElement; node is not null; node = node.Parent)
        {
            if (ReferenceEquals(node, _screen.Root))
            {
                return;
            }
        }

        if (!IsModalShown(app))
        {
            app.Focus(_screen.FocusTarget);
        }
    }

    private async Task NavigatePendingAsync(CancellationToken cancellationToken)
    {
        if (_pendingNavigation is not { } pending)
        {
            return;
        }

        _pendingNavigation = null;

        if (pending.RunInPlace)
        {
            ITuiScreen target = _screens[pending.Screen];
            await target.LoadAsync(cancellationToken);
            TuiCommandResultModel result = await CreateScreenCommandSet(target)
                .ExecuteAsync(pending.Command, cancellationToken);
            if (!result.IsError)
            {
                await _screen.LoadAsync(cancellationToken);
            }

            Notify(result);
            return;
        }

        if (_screen.Kind != pending.Screen)
        {
            SetOnShow(_screen, false);
            _screen = _screens[pending.Screen];
            SetOnShow(_screen, true);
            _currentScreen.Value = pending.Screen;
            _message.Value = TuiCommandResultModel.None;
            SetCommands();
            await _screen.LoadAsync(cancellationToken);
            _focusAfterNavigation = true;
        }

        if (pending.Command.Length == 0)
        {
            return;
        }

        _transientOpened = false;
        TuiCommandResultModel dispatched = await CreateScreenCommandSet(_screen)
            .ExecuteAsync(pending.Command, cancellationToken);
        if (_transientOpened && pending.ReturnScreen is { } returnScreen)
        {
            _returnScreens.Push(returnScreen);
        }

        Notify(dispatched);
    }

    private void SetCommands()
    {
        _commands.Clear();
        _commands.Add(new TuiCommandModel("quit", QuitAsync) { Aliases = ["q", "exit"] });
        foreach ((TuiScreen screen, string name, string alias) in Navigations)
        {
            _commands.Add(NavigationCommand(screen, name, alias));
        }

        _commands.Add(new TuiCommandModel("settings", (argument, _) => OpenSettings(argument))
        {
            Aliases = ["set"],
            AllowPrefixMatch = false
        });
        _commands.Add(new TuiCommandModel("theme", ThemeAsync)
        {
            AllowPrefixMatch = false,
            CompleteArgument = CompleteTheme
        });
        _commands.Add(new TuiCommandModel("quickstart", async (argument, token) =>
            {
                if (argument.Length > 0)
                {
                    return TuiCommandResultModel.Failed("usage: quickstart");
                }

                try { await _quickStart.BeginAsync(token); }
                catch (Exception exception) when (RequestScreen.IsRecoverable(exception))
                {
                    return TuiCommandResultModel.Failed($"quick start could not load: {FirstLine(exception.Message)}");
                }
                RestartRequested = true;
                return TuiCommandResultModel.None;
            })
            { AllowPrefixMatch = false });
        foreach (TuiCommandModel command in _screen.PromptCommands)
        {
            _commands.Add(command);
        }
    }

    private Task<TuiCommandResultModel> OpenSettings(string argument)
    {
        if (argument.Length > 0)
        {
            return Task.FromResult(TuiCommandResultModel.Failed("usage: settings"));
        }

        if (!_editor.IsConfigured)
        {
            return Task.FromResult(TuiCommandResultModel.Failed("settings: no default editor is configured"));
        }

        RequestExternalAction(new TuiExternalActionModel(EditSettingsAsync, _screen.FocusTarget));
        return Task.FromResult(TuiCommandResultModel.None);
    }

    private async Task<TuiCommandResultModel> EditSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            string path = await _settingsService.EnsureFileAsync(cancellationToken);
            await _editor.EditFileAsync(path, cancellationToken);
        }
        catch (ExternalEditorException exception)
        {
            return TuiCommandResultModel.Failed($"settings: {exception.Message}");
        }

        int previousKeybindVersion = StraumrKeybinds.Version;
        await _settingsService.LoadAsync(cancellationToken);
        bool themeChanged = _themeSelection.Apply();
        RestartRequested = themeChanged || previousKeybindVersion != StraumrKeybinds.Version;

        if (_themeSelection.Message is { } problem)
        {
            return TuiCommandResultModel.Failed(problem);
        }

        return themeChanged
            ? TuiCommandResultModel.Ok($"theme {StraumrStyleService.ThemeName}")
            : TuiCommandResultModel.None;
    }

    private async Task<TuiCommandResultModel> ThemeAsync(string argument, CancellationToken cancellationToken)
    {
        string text = argument.Trim();
        if (text.Length == 0)
        {
            return TuiCommandResultModel.Ok($"theme {StraumrStyleService.ThemeName}");
        }

        string[] parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["export", ..])
        {
            return Export(parts.Length > 1 ? parts[1] : null);
        }

        if (!TuiCommandArgumentHelpers.TryParseSingle(text, out string reference, out string? problem))
        {
            return TuiCommandResultModel.Failed($"theme: {problem}");
        }

        if (!StraumrThemeService.TryResolve(reference, _settingsService.SettingsDirectory, out _, out string? error))
        {
            return TuiCommandResultModel.Failed(error!);
        }

        int previousKeybindVersion = StraumrKeybinds.Version;
        try
        {
            await _settingsService.SetThemeAsync(reference, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TomlException)
        {
            return TuiCommandResultModel.Failed($"theme not saved: {FirstLine(exception.Message)}");
        }

        bool themeChanged = _themeSelection.Apply();
        if (!themeChanged && previousKeybindVersion == StraumrKeybinds.Version)
        {
            return TuiCommandResultModel.Ok($"theme {StraumrStyleService.ThemeName}");
        }

        RestartRequested = true;
        return _themeSelection.Message is { } notice
            ? TuiCommandResultModel.Failed(notice)
            : TuiCommandResultModel.Ok($"theme {StraumrStyleService.ThemeName}");
    }

    private TuiCommandResultModel Export(string? name)
    {
        string source = StraumrThemeService.BuiltInSource(BuiltInThemeHelpers.StraumrName)!;
        string file = string.IsNullOrWhiteSpace(name) ? "my-theme" : name.Trim();
        if (file.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
        {
            file = file[..^5];
        }

        string target = Path.Combine(_settingsService.SettingsDirectory, "themes", $"{file.ToLowerInvariant()}.toml");

        try
        {
            if (File.Exists(target))
            {
                return TuiCommandResultModel.Failed(
                    $"{PathFormatting.Display(target)} already exists; :theme export <name> writes another");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, source);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return TuiCommandResultModel.Failed($"export failed: {exception.Message}");
        }

        return TuiCommandResultModel.Ok($"wrote {PathFormatting.Display(target)}");
    }

    private static string FirstLine(string message)
    {
        int end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message : message[..end];
    }

    private TuiCommandCompletionModel CompleteTheme(string argument, int caret)
    {
        string text = argument[..Math.Clamp(caret, 0, argument.Length)];
        return text.StartsWith("export ", StringComparison.Ordinal)
            ? TuiCommandCompletionModel.None
            : TuiCommandSetModel.Match([.. StraumrThemeService.BuiltInNames, .. Installed(), "export"], text, 0);
    }

    private IEnumerable<string> Installed() => StraumrThemeService
        .Installed(_settingsService.SettingsDirectory)
        .Select(theme => theme.Name)
        .Where(name => !StraumrThemeService.BuiltInNames.Contains(name, StringComparer.OrdinalIgnoreCase));

    private IEnumerable<MenuItem> BuildScreenMenu() =>
        Navigations
            .OrderBy(navigation => navigation.Screen)
            .Select(navigation => new MenuItem(
                    new TextBlock(navigation.Screen.ToString().ToLowerInvariant())
                        .Style(StraumrStyleService.BrightText))
                .Icon(new TextBlock(navigation.Screen == _currentScreen.Value ? "●" : " ")
                    .Style(StraumrStyleService.AccentText))
                .Shortcut(new TextBlock($":{navigation.Alias}").Style(StraumrStyleService.MutedText))
                .Action(() => _ = QueueNavigation(navigation.Screen, string.Empty)))
            .Append(new MenuItem(new TextBlock("Settings").Style(StraumrStyleService.BrightText))
                .Icon(new TextBlock(" ").Style(StraumrStyleService.MutedText))
                .Shortcut(new TextBlock(":settings").Style(StraumrStyleService.MutedText))
                .Action(() => _submitted.Enqueue("settings")));

    private TuiCommandModel NavigationCommand(TuiScreen screen, string name, string shortAlias) =>
        new(name, (argument, _) => QueueNavigation(screen, argument))
        {
            Aliases = [shortAlias],
            AllowPrefixMatch = false,
            CompleteArgument = (argument, caret) => CreateScreenCommandSet(_screens[screen]).Complete(argument, caret)
        };

    private Task<TuiCommandResultModel> QueueNavigation(TuiScreen screen, string command)
    {
        bool fromElsewhere = _screen.Kind != screen;
        TuiCommandModel? destinationCommand = CreateScreenCommandSet(_screens[screen]).ResolveCommand(command);
        bool runInPlace = fromElsewhere && destinationCommand is { RunsInPlaceFromOtherScreens: true };
        _pendingNavigation = new TuiPendingNavigationModel(screen, command, runInPlace,
            fromElsewhere ? _screen.Kind : null);
        return Task.FromResult(TuiCommandResultModel.None);
    }

    private void ReturnFromTransientScreen()
    {
        if (_returnScreens.TryPop(out TuiScreen screen))
        {
            _pendingNavigation = new TuiPendingNavigationModel(screen, string.Empty, false, null);
        }
    }

    private static TuiCommandSetModel CreateScreenCommandSet(ITuiScreen screen)
    {
        var commands = new TuiCommandSetModel();
        foreach (TuiCommandModel command in screen.PromptCommands)
        {
            commands.Add(command);
        }

        return commands;
    }

    private async Task SetActiveWorkspaceAsync(string? name, CancellationToken cancellationToken)
    {
        if (_workspaceContextLoaded && _activeWorkspaceName.Value == name)
        {
            return;
        }

        _workspaceContextLoaded = true;
        _activeWorkspaceName.Value = name;

        foreach (ITuiScreen screen in _screens.Values.Where(screen => !ReferenceEquals(screen, _screen)))
        {
            await screen.LoadAsync(cancellationToken);
        }
    }

    public void RequestExit() => ExitRequested = true;

    private Command BuildInterruptCommand() =>
        new()
        {
            Id = "Straumr.Interrupt",
            LabelMarkup = "Cancel",
            Gesture = TuiKeybindHelpers.Get("Straumr.Interrupt"),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.None,
            Execute = _ => RequestInterrupt()
        };

    private static Command BuildFocusPreviousCommand(TerminalApp app) =>
        new()
        {
            Id = "Straumr.FocusPrevious",
            LabelMarkup = "Previous field",
            Gesture = TuiKeybindHelpers.Get("Straumr.FocusPrevious"),
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
        TuiExternalActionModel? action = _pendingExternalAction;
        _pendingExternalAction = null;
        if (action is null)
        {
            return;
        }

        Notify(await action.ExecuteAsync(cancellationToken));
        _focusOnAttach = action.FocusTarget;
    }

    private void AttachTo(TerminalApp app)
    {
        if (ReferenceEquals(_app, app))
        {
            return;
        }

        _app = app;
        TuiWindowHelpers.AttachTo(app);

        app.Root.SetStyle(Theme.Key, StraumrStyleService.FrameworkTheme);

        app.RemoveGlobalCommand(TerminalApp.DefaultQuitCommandId);
        foreach (Command command in BuildOpenPromptCommands())
        {
            app.AddGlobalCommand(command);
        }

        if (TuiKeybindHelpers.Get(OpenPromptCommandId) != new KeyGesture(':'))
        {
            foreach (TerminalModifiers modifiers in new[] { TerminalModifiers.None, TerminalModifiers.Shift })
            {
                app.AddGlobalCommand(new Command
                {
                    Id = "Straumr.Colon." + modifiers,
                    LabelMarkup = "Command",
                    Gesture = new KeyGesture(':', modifiers),
                    Presentation = CommandPresentation.None,
                    CanExecute = _ => !_quickStart.Active && !_prompt.IsOpen && !IsModalOpen,
                    ConsumesGestureWhenUnavailable = false,
                    Execute = _ => OpenPrompt()
                });
            }
        }
        app.AddGlobalCommand(BuildInterruptCommand());
        if (_quickStart.Active)
        {
            return;
        }

        app.AddGlobalCommand(BuildFocusPreviousCommand(app));
        ConfigureFocusTraversal(app, "Straumr.FocusNext", new KeyGesture(TerminalKey.Tab), 1);
        ConfigureFocusTraversal(app, "Straumr.FocusPreviousTab", new KeyGesture(TerminalKey.Tab, TerminalModifiers.Shift), -1);

        if (_focusOnAttach is not { } focusTarget)
        {
            return;
        }

        _focusOnAttach = null;
        app.Focus(focusTarget);
    }

    private static void ConfigureFocusTraversal(TerminalApp app, string id, KeyGesture original, int step)
    {
        KeyGesture? gesture = TuiKeybindHelpers.Get(id);
        if (gesture == original)
        {
            return;
        }

        app.AddGlobalCommand(new Command
        {
            Id = id,
            LabelMarkup = step > 0 ? "Next field" : "Previous field",
            Gesture = gesture,
            Presentation = CommandPresentation.None,
            Execute = _ => app.FocusStep(step)
        });
        app.AddGlobalCommand(new Command
        {
            Id = $"{id}.Replaced",
            LabelMarkup = "",
            Gesture = original,
            Presentation = CommandPresentation.None,
            Execute = _ => { }
        });
    }

    private void RequestExternalAction(TuiExternalActionModel action)
    {
        _pendingExternalAction ??= action;
    }

    private Visual BuildMessageLine() =>
        new TextBlock(() => _message.Value.Message ?? string.Empty)
            .Style(() => _message.Value.IsError
                ? StraumrStyleService.RedText
                : StraumrStyleService.MutedText)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

    private IEnumerable<Command> BuildOpenPromptCommands()
    {
        yield return BuildOpenPromptCommand(
            OpenPromptCommandId,
            CommandPresentation.CommandBar);
        yield return BuildOpenPromptCommand(
            $"{OpenPromptCommandId}.Shifted",
            CommandPresentation.None);
    }

    private Command BuildOpenPromptCommand(
        string id,
        CommandPresentation presentation) =>
        new()
        {
            Id = id,
            LabelMarkup = "Command",
            Gesture = TuiKeybindHelpers.Get(id),
            Importance = CommandImportance.Secondary,
            Presentation = presentation,
            CanExecute = _ => !_quickStart.Active && !_prompt.IsOpen && !IsModalOpen,
            IsVisible = _ => !_quickStart.Active && !IsModalOpen,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => OpenPrompt()
        };

    private bool IsModalShown(TerminalApp app) =>
        app.Root.EnumerateVisualsDepthFirst().Any(visual =>
            visual is IModalVisual { IsModal: true } && !ReferenceEquals(visual, _prompt.Root));

    private void OpenPrompt()
    {
        _message.Value = TuiCommandResultModel.None;
        _prompt.Open();
    }

    private async Task RunSubmittedCommandsAsync(CancellationToken cancellationToken)
    {
        while (_submitted.Count > 0)
        {
            Notify(await _commands.ExecuteAsync(_submitted.Dequeue(), cancellationToken));
        }
    }

    private void AnnouncePending()
    {
        if (_pendingAnnouncement is not { } announcement)
        {
            return;
        }

        _pendingAnnouncement = null;
        Notify(announcement);
    }

    private void Notify(TuiCommandResultModel result)
    {
        _message.Value = result;
        if (result.Message is not null)
        {
            _messageExpiry = DateTimeOffset.UtcNow + MessageLifetime;
        }
    }

    private void ExpireMessage()
    {
        if (_message.Value.Message is not null && DateTimeOffset.UtcNow >= _messageExpiry)
        {
            _message.Value = TuiCommandResultModel.None;
        }
    }

    private Task<TuiCommandResultModel> QuitAsync(string argument, CancellationToken cancellationToken)
    {
        if (argument.Length > 0)
        {
            return Task.FromResult(TuiCommandResultModel.Failed("usage: quit"));
        }

        RequestExit();
        return Task.FromResult(TuiCommandResultModel.None);
    }
}
