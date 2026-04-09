using Microsoft.Extensions.DependencyInjection;
using Straumr.Console.Shared.Theme;
using Straumr.Console.Tui.Screens.Base;

namespace Straumr.Console.Tui.Infrastructure;

internal sealed class ScreenEngine
{
    private readonly IServiceProvider _serviceProvider;
    private readonly StraumrTheme _theme;

    public ScreenEngine(IServiceProvider serviceProvider, StraumrTheme theme)
    {
        _serviceProvider = serviceProvider;
        _theme = theme;
    }

    public async Task RunAsync<TScreen>(CancellationToken cancellationToken) where TScreen : Screen
    {
        using var app = new TuiApp(_theme);
        Type? nextScreen = typeof(TScreen);

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
            app.RunLoop();

            nextScreen = requestedNavigation;
        }
    }
}
