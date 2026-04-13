using System.Diagnostics.CodeAnalysis;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Base;
using Straumr.Console.Tui.Helpers;
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
    private EventHandler<Key>? _keyHandler;

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

        _scheme = ColorResolver.BuildScheme(theme);

        _window = new Window
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.None,
        };
        _window.SetScheme(_scheme);
    }

    internal void LoadScreen(Screen screen)
    {
        _window.RemoveAll();

        if (_keyHandler is not null)
        {
            _window.KeyDown -= _keyHandler;
            _keyHandler = null;
        }

        _keyHandler = (_, key) =>
        {
            if (screen.OnKeyDown(key))
            {
                key.Handled = true;
            }
        };

        _window.KeyDown += _keyHandler;

        screen.QuitAction = _application.RequestStop;

        foreach (TuiComponent component in screen.Components)
        {
            _window.Add(component.Build());
        }
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

        EventHandler<Key> handler = (_, key) =>
        {
            if (screen.OnKeyDown(key))
            {
                key.Handled = true;
            }
        };

        promptWindow.KeyDown += handler;
        screen.QuitAction = _application.RequestStop;

        foreach (TuiComponent component in screen.Components)
        {
            promptWindow.Add(component.Build());
        }

        _application.Run(promptWindow);

        promptWindow.KeyDown -= handler;
        promptWindow.Dispose();

        return screen.Result;
    }

    public void Dispose()
    {
        _window.Dispose();
        _application.Dispose();
    }
}
