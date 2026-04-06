using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Request;

public class RequestDeleteCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService)
    : AsyncCommand<RequestDeleteCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, optionsService, workspaceService);
            if (resolved is null)
            {
                AnsiConsole.MarkupLine($"[red]Workspace not found: {Markup.Escape(settings.Workspace)}[/]");
                return 1;
            }

            optionsService.Options.CurrentWorkspace = resolved;
        }

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

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }
}