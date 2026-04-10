using Straumr.Console.Shared.Theme;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class TuiAppResolver
{
    private TuiApp? Current { get; set; }

    public TuiApp GetOrCreate(StraumrTheme theme, out bool ownsApp)
    {
        if (Current is not null)
        {
            ownsApp = false;
            return Current;
        }

        ownsApp = true;
        Current = new TuiApp(theme);
        return Current;
    }

    public void Clear(TuiApp app)
    {
        if (ReferenceEquals(Current, app))
        {
            Current = null;
        }
    }
}
