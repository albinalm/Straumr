using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Request;

public class RequestDeleteCommand(IStraumrRequestService requestService)
    : AsyncCommand<RequestDeleteCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        await requestService.DeleteAsync(settings.Identifier);
        AnsiConsole.MarkupLine($"[red]Deleted request[/] [bold]{settings.Identifier}[/]");
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")] public required string Identifier { get; set; }
    }
}