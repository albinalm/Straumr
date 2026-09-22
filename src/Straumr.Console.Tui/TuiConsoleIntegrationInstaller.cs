namespace Straumr.Console.Tui;

public sealed class TuiConsoleIntegrationInstaller : IConsoleIntegrationInstaller
{
    public void Install(IConsoleIntegrationBuilder builder)
    {
        builder.AddIntegration(new TuiConsoleIntegration());
    }
}
