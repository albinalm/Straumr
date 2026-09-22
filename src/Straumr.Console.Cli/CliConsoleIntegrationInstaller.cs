namespace Straumr.Console.Cli;

public sealed class CliConsoleIntegrationInstaller : IConsoleIntegrationInstaller
{
    public void Install(IConsoleIntegrationBuilder builder) => builder.AddIntegration(new CliConsoleIntegration());
}
