using System.Diagnostics.CodeAnalysis;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Components.Base;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Screens.Base;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui;

public class TuiApp
{
    private readonly StraumrTheme _theme;

    public TuiApp(StraumrTheme theme)
    {
        _theme = theme;
    }

    [UnconditionalSuppressMessage("AOT",
        "IL2026:Using member 'Terminal.Gui.App.IApplication.Init(String)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code.",
        Justification = "TUI mode is a lightweight UI test surface; trimming impact is acceptable.")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Using member 'Terminal.Gui.App.IApplication.Init(String)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling.",
        Justification = "TUI mode is a lightweight UI test surface; dynamic code use is acceptable.")]
    public void Run(Screen screen)
    {
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;
        System.Console.InputEncoding = System.Text.Encoding.UTF8;

        using IApplication app = Application.Create();
        app.Init();

        Scheme scheme = ColorResolver.BuildScheme(_theme);

        using Window window = new();
        window.Width = Dim.Fill();
        window.Height = Dim.Fill();
        window.BorderStyle = LineStyle.None;
        window.SetScheme(scheme);

        screen.QuitAction = app.RequestStop;

        window.KeyDown += (_, key) =>
        {
            if (screen.OnKeyDown(key))
            {
                key.Handled = true;
            }
        };

        foreach (TuiComponent component in screen.Components)
        {
            window.Add(component.Build());
        }

        app.Run(window);
    }
}
