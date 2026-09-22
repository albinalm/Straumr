using System.Text.Json;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Request;

[UsedImplicitly]
public class RequestListCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService)
    : AsyncCommand<RequestListCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, RequestListCommandSettings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? workspaceEntry = stateService.State.CurrentWorkspace;

        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, workspaceService);
            if (resolved is null)
            {
                WriteError($"Workspace not found: {settings.Workspace}", settings.Json);
                return 1;
            }

            workspaceEntry = resolved;
        }

        if (workspaceEntry == null)
        {
            WriteError("No workspace loaded. Please load a workspace using 'workspace use <name>'", settings.Json);
            return 1;
        }

        StraumrWorkspace workspace = await workspaceService.GetAsync(
            workspaceEntry.Id, true, cancellation);

        List<RequestListEntryModel> entries = new();
        foreach (Guid requestGuid in workspace.Requests)
        {
            RequestListEntryModel requestEntry = await GetRequest(requestGuid, workspaceEntry);
            entries.Add(requestEntry);
        }

        if (!string.IsNullOrEmpty(settings.Filter))
        {
            entries = entries.Where(e =>
                e.Request?.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase) == true ||
                e.Id.ToString().StartsWith(settings.Filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (settings.Json)
        {
            RequestListItem[] items = entries.Select(e => new RequestListItem(
                e.Id.ToString(),
                e.Request?.Name ?? "N/A",
                e.Request?.Method.Method ?? "N/A",
                e.Request?.Uri ?? "N/A",
                StripMarkup(e.Status),
                e.Request?.LastAccessed.LocalDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
            )).ToArray();
            System.Console.WriteLine(JsonSerializer.Serialize(items, CliJsonContext.Relaxed.RequestListItemArray));
            return 0;
        }

        if (entries.Count == 0)
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

        foreach (RequestListEntryModel entry in entries)
        {
            table.AddRow(
                entry.Id.ToString(),
                Markup.Escape(entry.Request?.Name ?? "N/A"),
                entry.Request?.Method.Method ?? "N/A",
                entry.Request?.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "N/A",
                entry.Status);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<RequestListEntryModel> GetRequest(Guid requestId, StraumrWorkspaceEntry workspaceEntry)
    {
        string status;
        StraumrRequest? request = null;
        try
        {
            request = await requestService.GetAsync(workspaceEntry, requestId);
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

        return new RequestListEntryModel
        {
            Id = requestId,
            Request = request,
            Status = status
        };
    }

    private static string StripMarkup(string value) =>
        Regex.Replace(value, @"\[.*?\]", string.Empty);
}
