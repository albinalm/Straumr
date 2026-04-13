using Terminal.Gui.App;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class TuiApplicationContext
{
    public IApplication? Application { get; internal set; }
}
