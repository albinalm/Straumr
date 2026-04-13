using Straumr.Console.Shared.Theme;

namespace Straumr.Console.Tui.Infrastructure;

public sealed class TuiAppResolver
{
    private TuiApp? Current { get; set; }
    private readonly TuiApplicationContext _context;

    public TuiAppResolver(TuiApplicationContext context)
    {
        _context = context;
    }

    public TuiApp GetOrCreate(StraumrTheme theme, out bool ownsApp)
    {
        if (Current is not null)
        {
            ownsApp = false;
            _context.Application = Current.ApplicationInstance;
            return Current;
        }

        ownsApp = true;
        Current = new TuiApp(theme);
        _context.Application = Current.ApplicationInstance;
        return Current;
    }

    public void Clear(TuiApp app)
    {
        if (ReferenceEquals(Current, app))
        {
            Current = null;
            _context.Application = null;
        }
    }
}
