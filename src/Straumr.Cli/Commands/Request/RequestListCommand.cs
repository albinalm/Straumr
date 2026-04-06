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
using static Straumr.Cli.Helpers.ErrorOutput;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Request;

public class RequestListCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService)
    : AsyncCommand<RequestListCommand.Settings>
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
                Write($"Workspace not found: {settings.Workspace}", settings.Json);
                return 1;
            }

            optionsService.Options.CurrentWorkspace = resolved;
        }

        StraumrWorkspaceEntry? workspaceEntry = optionsService.Options.CurrentWorkspace;
        if (workspaceEntry == null)
        {
            Write("No workspace loaded. Please load a workspace using 'workspace use <name>'", settings.Json);
            return 1;
        }

        StraumrWorkspace workspace = await workspaceService.GetWorkspace(workspaceEntry.Path);

        var entries = new List<RequestListEntry>();
        foreach (Guid requestGuid in workspace.Requests)
        {
            RequestListEntry requestEntry = await GetRequest(requestGuid);
            entries.Add(requestEntry);
        }

        if (!string.IsNullOrEmpty(settings.Filter))
        {
            entries = entries.Where(e =>
                (e.Request?.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase) == true) ||
                e.Id.ToString().StartsWith(settings.Filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (settings.Json)
        {
            var items = entries.Select(e => new RequestListItem(
                Id: e.Id.ToString(),
                Name: e.Request?.Name ?? "N/A",
                Method: e.Request?.Method.Method ?? "N/A",
                Uri: e.Request?.Uri ?? "N/A",
                Status: StripMarkup(e.Status),
                LastAccessed: e.Request?.LastAccessed.LocalDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
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

        foreach (RequestListEntry entry in entries)
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
            Id = requestId,
            Request = request,
            Status = status
        };
    }

    private static string StripMarkup(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"\[.*?\]", string.Empty);

    private class RequestListEntry
    {
        public Guid Id { get; init; }
        public StraumrRequest? Request { get; init; }
        public required string Status { get; init; }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-j|--json")]
        [Description("Output as JSON array")]
        public bool Json { get; set; }

        [CommandOption("--filter")]
        [Description("Filter results by name (substring) or ID prefix")]
        public string? Filter { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }
}
