using Microsoft.Extensions.DependencyInjection;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Screens.Base;

namespace Straumr.Console.Tui.Infrastructure;

internal sealed class ScreenEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly StraumrTheme _theme;
    private readonly TuiAppResolver _appResolver;

    public ScreenEngine(IServiceProvider serviceProvider, StraumrTheme theme, TuiAppResolver appResolver)
    {
        _serviceProvider = serviceProvider;
        _theme = theme;
        _appResolver = appResolver;
    }

    public async Task RunAsync<TScreen>(CancellationToken cancellationToken) where TScreen : Screen
    {
        TuiApp app = _appResolver.GetOrCreate(_theme, out bool ownsApp);
        Type? nextScreen = typeof(TScreen);

        try
        {
            while (!cancellationToken.IsCancellationRequested && nextScreen is { } screenType)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using IServiceScope scope = _serviceProvider.CreateScope();
                var screen = (Screen)scope.ServiceProvider.GetRequiredService(screenType);

                Type? requestedNavigation = null;
                screen.NavigateAction = type => requestedNavigation = type;
                screen.QuitAction = app.RequestStop;

                await screen.InitializeAsync(cancellationToken);

                if (requestedNavigation is not null)
                {
                    nextScreen = requestedNavigation;
                    continue;
                }

                app.LoadScreen(screen);
                using CancellationTokenRegistration registration = cancellationToken.Register(app.RequestStop);
                app.RunLoop();

                nextScreen = requestedNavigation;
            }
        }
        finally
        {
            if (ownsApp)
            {
                app.Dispose();
                _appResolver.Clear(app);
            }
        }
    }
}
