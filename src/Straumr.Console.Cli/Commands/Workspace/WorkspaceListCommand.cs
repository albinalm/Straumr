using System.ComponentModel;
using System.Text.Json;
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

public class WorkspaceListCommand(IStraumrOptionsService optionsService, IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceListCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        List<StraumrWorkspaceEntry> workspaceEntries = optionsService.Options.Workspaces;

        List<WorkspaceListEntry> workspaceListItems = [];

        foreach (StraumrWorkspaceEntry entry in workspaceEntries)
        {
            WorkspaceListEntry workspace = await GetWorkspaceListEntry(entry);
            workspaceListItems.Add(workspace);
        }

        if (!string.IsNullOrEmpty(settings.Filter))
        {
            workspaceListItems = workspaceListItems.Where(e =>
                (e.Workspace?.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase) == true) ||
                e.Entry.Id.ToString().StartsWith(settings.Filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (settings.Json)
        {
            WorkspaceListItem[] items = workspaceListItems.Select(e => new WorkspaceListItem(
                Id: e.Entry.Id.ToString(),
                Name: e.Workspace?.Name ?? "N/A",
                Path: e.Entry.Path,
                IsCurrent: e.IsCurrent,
                Requests: e.Workspace?.Requests.Count ?? 0,
                Status: StripMarkup(e.Status),
                LastAccessed: e.LastAccessed?.LocalDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
            )).ToArray();
            System.Console.WriteLine(JsonSerializer.Serialize(items, CliJsonContext.Relaxed.WorkspaceListItemArray));
            return 0;
        }

        if (workspaceListItems.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No workspaces found.[/]");
            return 0;
        }

        Table table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Last Accessed");
        table.AddColumn("Requests");
        table.AddColumn("Status");

        foreach (WorkspaceListEntry workspaceListItem in workspaceListItems.OrderByDescending(x => x.LastAccessed))
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

    private async Task<WorkspaceListEntry> GetWorkspaceListEntry(StraumrWorkspaceEntry entry)
    {
        string status;
        StraumrWorkspace? workspace = null;
        try
        {
            workspace = await workspaceService.PeekWorkspaceAsync(entry.Path);
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
            LastAccessed = workspace?.LastAccessed,
            IsCurrent = optionsService.Options.CurrentWorkspace?.Id == entry.Id
        };
    }

    private static string StripMarkup(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"\[.*?\]", string.Empty);

    private class WorkspaceListEntry
    {
        public bool IsCurrent { get; init; }
        public StraumrWorkspace? Workspace { get; init; }
        public required StraumrWorkspaceEntry Entry { get; init; }
        public required string Status { get; init; }
        public DateTimeOffset? LastAccessed { get; init; }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-j|--json")]
        [Description("Output as JSON array")]
        public bool Json { get; set; }

        [CommandOption("--filter")]
        [Description("Filter results by name (substring) or ID prefix")]
        public string? Filter { get; set; }
    }
}
