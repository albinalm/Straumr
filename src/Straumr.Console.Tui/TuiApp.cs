using System.Diagnostics.CodeAnalysis;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Base;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Infrastructure;
using Straumr.Console.Tui.Screens.Base;
using Straumr.Console.Tui.Screens.Prompts;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui;

public sealed class TuiApp : IDisposable
{
    private readonly IApplication _application;
    private readonly Window _window;
    private readonly Scheme _scheme;
    private readonly KeyPrefilter _keyPrefilter;
    private readonly KeyDiagnostics _keyDiagnostics;
    private Func<Key, bool>? _rootHandler;

    [UnconditionalSuppressMessage("AOT",
        "IL2026:Using member 'Terminal.Gui.App.IApplication.Init(String)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code.",
        Justification = "TUI mode is a lightweight UI test surface; trimming impact is acceptable.")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Using member 'Terminal.Gui.App.IApplication.Init(String)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling.",
        Justification = "TUI mode is a lightweight UI test surface; dynamic code use is acceptable.")]
    public TuiApp(StraumrTheme theme)
    {
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;
        System.Console.InputEncoding = System.Text.Encoding.UTF8;

        _application = Application.Create();
        _application.Init();

        _application.Driver?.Force16Colors = !ColorResolver.ThemeHasTrueColor(theme);

        _scheme = ColorResolver.BuildScheme(theme);

        _window = new Window
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.None,
        };
        _window.SetScheme(_scheme);

        _keyDiagnostics = new KeyDiagnostics();
        _keyPrefilter = new KeyPrefilter(_application, _keyDiagnostics);
    }

    internal IApplication ApplicationInstance => _application;

    internal void LoadScreen(Screen screen)
    {
        _window.RemoveAll();

        if (_rootHandler is not null)
        {
            _keyPrefilter.Pop();
            _rootHandler = null;
        }

        _rootHandler = screen.OnKeyDown;
        _keyPrefilter.Push(screen.GetType().Name, _rootHandler);
        _keyPrefilter.RecordEvent($"LOAD {screen.GetType().Name}");

        screen.QuitAction = _application.RequestStop;

        foreach (TuiComponent component in screen.Components)
        {
            _window.Add(component.Build());
        }

        _keyDiagnostics.AttachTo(_window);
    }

    internal void RunLoop() => _application.Run(_window);

    internal void RequestStop() => _application.RequestStop();

    internal TResult? RunPrompt<TResult>(PromptScreen<TResult> screen)
    {
        Window promptWindow = new()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.None,
        };
        promptWindow.SetScheme(_scheme);

        Func<Key, bool> handler = screen.OnKeyDown;
        _keyPrefilter.Push(screen.GetType().Name, handler);
        screen.QuitAction = _application.RequestStop;

        foreach (TuiComponent component in screen.Components)
        {
            promptWindow.Add(component.Build());
        }

        _keyDiagnostics.AttachTo(promptWindow);

        try
        {
            _keyPrefilter.RecordEvent($"RUN {screen.GetType().Name} begin");
            _application.Run(promptWindow);
        }
        finally
        {
            _keyPrefilter.RecordEvent($"RUN {screen.GetType().Name} end");
            _keyPrefilter.Pop();
            promptWindow.Dispose();
            _keyPrefilter.RecordEvent($"DISPOSE {screen.GetType().Name}");
            _keyDiagnostics.AttachTo(_window);
        }

        return screen.Result;
    }

    public void Dispose()
    {
        _keyPrefilter.Dispose();
        _window.Dispose();
        _application.Dispose();
    }
}
