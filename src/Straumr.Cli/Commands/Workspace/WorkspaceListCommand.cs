using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Workspace;

public class WorkspaceListCommand(IStraumrOptionsService optionsService, IStraumrWorkspaceService workspaceService)
    : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellation)
    {
        List<StraumrWorkspaceEntry> workspaceEntries = optionsService.Options.Workspaces;

        if (workspaceEntries.Count == 0)
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

        List<WorkspaceListEntry> workspaceListItems = [];

        foreach (StraumrWorkspaceEntry entry in workspaceEntries)
        {
            WorkspaceListEntry workspace = await GetWorkspaceListEntry(entry);
            workspaceListItems.Add(workspace);
        }

        foreach (WorkspaceListEntry workspaceListItem in workspaceListItems.OrderByDescending(x => x.LastAccessed))
        {
            table.AddRow(
                workspaceListItem.Entry.Id.ToString(),
                Markup.Escape(workspaceListItem.Workspace?.Name ?? "N/A"),
                workspaceListItem.LastAccessed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "N/A",
                workspaceListItem.Workspace?.Requests.Count.ToString() ?? "N/A",
                workspaceListItem.Status);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<WorkspaceListEntry> GetWorkspaceListEntry(StraumrWorkspaceEntry entry)
    {
        string status;
        StraumrWorkspace? workspace = null;
        try
        {
            workspace = await workspaceService.PeekWorkspace(entry.Path);
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

        return new WorkspaceListEntry
        {
            Workspace = workspace,
            Entry = entry,
            Status = status,
            LastAccessed = workspace?.LastAccessed
        };
    }

    private class WorkspaceListEntry
    {
        public StraumrWorkspace? Workspace { get; init; }
        public required StraumrWorkspaceEntry Entry { get; init; }
        public required string Status { get; init; }
        public DateTimeOffset? LastAccessed { get; init; }
    }
}