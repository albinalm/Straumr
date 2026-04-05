using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Request;

public class RequestListCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService)
    : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? workspaceEntry = optionsService.Options.CurrentWorkspace;
        if (workspaceEntry == null)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        StraumrWorkspace workspace = await workspaceService.GetWorkspace(workspaceEntry.Path);
        if (workspace.Requests.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No requests found.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Method");
        table.AddColumn("Last Accessed");
        table.AddColumn("Status");

        foreach (Guid requestGuid in workspace.Requests)
        {
            RequestListEntry requestEntry = await GetRequest(requestGuid);
            table.AddRow(
                requestGuid.ToString(),
                Markup.Escape(requestEntry.Request?.Name ?? "N/A"),
                requestEntry.Request?.Method.ToString() ?? "N/A",
                requestEntry.Request?.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "N/A",
                requestEntry.Status);
        }

        AnsiConsole.Write(table);
        return 0;
    }


    private async Task<RequestListEntry> GetRequest(Guid requestId)
    {
        string status;
        StraumrRequest? request = null;
        try
        {
            request = await requestService.PeekByIdAsync(requestId);
            status = "[green]Valid[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            status = "[red]Corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            status = "[yellow]Missing[/]";
        }

        return new RequestListEntry
        {
            Request = request,
            Status = status
        };
    }

    private class RequestListEntry
    {
        public StraumrRequest? Request { get; init; }
        public required string Status { get; init; }
    }
}