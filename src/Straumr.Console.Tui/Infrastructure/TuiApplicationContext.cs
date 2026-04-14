using Straumr.Console.Tui.Screens.Prompts;
using Terminal.Gui.App;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class TuiApplicationContext
{
    public IApplication? Application { get; private set; }
    internal TuiApp? App { get; private set; }

    internal void Attach(TuiApp app)
    {
        App = app;
        Application = app.ApplicationInstance;
    }

    internal void Detach()
    {
        App = null;
        Application = null;
    }

    internal bool TryRunPrompt<TResult>(PromptScreen<TResult> screen, out TResult? result)
    {
        if (App is null)
        {
            result = default;
            return false;
        }

        result = App.RunPrompt(screen);
        return true;
    }
}
