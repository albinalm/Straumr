using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Infrastructure;
using Straumr.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Request;

public class RequestCopyCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService)
    : AsyncCommand<RequestCopyCommand.Settings>
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

        if (optionsService.Options.CurrentWorkspace is null)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        try
        {
            StraumrRequest copy = await requestService.CopyAsync(settings.Identifier, settings.NewName);

            if (settings.Json)
            {
                var result = new RequestCreateResult(copy.Id.ToString(), copy.Name, copy.Method.Method, copy.Uri);
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.RequestCreateResult));
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $"[green]Copied request[/] [bold]{Markup.Escape(settings.Identifier)}[/] to [bold]{Markup.Escape(settings.NewName)}[/]");
            }

            return 0;
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return -1;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Identifier>")]
        [Description("Name or ID of the request to copy")]
        public required string Identifier { get; set; }

        [CommandArgument(1, "<NewName>")]
        [Description("Name for the new request")]
        public required string NewName { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the result as JSON")]
        public bool Json { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }
}
