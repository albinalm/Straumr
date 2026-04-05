using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Services.Interfaces;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Request;

public class RequestDeleteCommand(IStraumrOptionsService optionsService, IStraumrRequestService requestService)
    : AsyncCommand<RequestDeleteCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        bool hasWorkspace = optionsService.Options.CurrentWorkspace != null;

        if (!hasWorkspace)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        await requestService.DeleteAsync(settings.Identifier);
        AnsiConsole.MarkupLine($"[red]Deleted request[/] [bold]{settings.Identifier}[/]");
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the request to delete")]
        public required string Identifier { get; set; }
    }
}