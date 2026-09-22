using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Workspace;

public class WorkspaceListCommand(IStraumrStateService stateService, IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceListCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WorkspaceListCommandSettings settings,
        CancellationToken cancellation)
    {
        List<StraumrWorkspaceEntry> workspaceEntries = stateService.State.Workspaces;

        List<WorkspaceListEntryModel> workspaceListItems = [];

        foreach (StraumrWorkspaceEntry entry in workspaceEntries)
        {
            WorkspaceListEntryModel workspace = await GetWorkspaceListEntry(entry);
            workspaceListItems.Add(workspace);
        }

        if (!string.IsNullOrEmpty(settings.Filter))
        {
            workspaceListItems = workspaceListItems.Where(e =>
                e.Workspace?.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase) == true ||
                e.Entry.Id.ToString().StartsWith(settings.Filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (settings.Json)
        {
            WorkspaceListItem[] items = workspaceListItems.Select(e => new WorkspaceListItem(
                e.Entry.Id.ToString(),
                e.Workspace?.Name ?? "N/A",
                e.Entry.Path,
                e.IsCurrent,
                e.Workspace?.Requests.Count ?? 0,
                StripMarkup(e.Status),
                e.LastAccessed?.LocalDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
            )).ToArray();
            System.Console.WriteLine(JsonSerializer.Serialize(items, CliJsonContext.Relaxed.WorkspaceListItemArray));
            return 0;
        }

        if (workspaceListItems.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No workspaces found.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Last Accessed");
        table.AddColumn("Requests");
        table.AddColumn("Status");

        foreach (WorkspaceListEntryModel workspaceListItem in workspaceListItems.OrderByDescending(x => x.LastAccessed))
        {
            string idString = workspaceListItem.IsCurrent
                ? $"[blue](Current)[/] {workspaceListItem.Entry.Id.ToString()}"
                : workspaceListItem.Entry.Id.ToString();

            table.AddRow(
                idString,
                Markup.Escape(workspaceListItem.Workspace?.Name ?? "N/A"),
                workspaceListItem.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "N/A",
                workspaceListItem.Workspace?.Requests.Count.ToString() ?? "N/A",
                workspaceListItem.Status);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<WorkspaceListEntryModel> GetWorkspaceListEntry(StraumrWorkspaceEntry entry)
    {
        string status;
        StraumrWorkspace? workspace = null;
        try
        {
            workspace = await workspaceService.GetAsync(entry.Id);
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

        return new WorkspaceListEntryModel
        {
            Workspace = workspace,
            Entry = entry,
            Status = status,
            LastAccessed = workspace?.LastAccessed,
            IsCurrent = stateService.State.CurrentWorkspace?.Id == entry.Id
        };
    }

    private static string StripMarkup(string value) =>
        Regex.Replace(value, @"\[.*?\]", string.Empty);
}
