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
        List<StraumrWorkspaceEntry> workspaces = optionsService.Options.Workspaces;

        if (workspaces.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No workspaces found.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Last Accessed");
        table.AddColumn("Status");

        foreach (StraumrWorkspaceEntry entry in workspaces.OrderByDescending(w => w.LastAccessed))
        {
            string status = await GetWorkspaceStatus(entry);

            table.AddRow(
                Markup.Escape(entry.Name),
                entry.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                status);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<string> GetWorkspaceStatus(StraumrWorkspaceEntry entry)
    {
        string status;

        try
        {
            await workspaceService.Load(entry.Name);
            status = "[green]Valid[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.FileCorrupt)
        {
            status = "[red]Invalid[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.FileNotFound)
        {
            status = "[yellow]Missing[/]";
        }

        return status;
    }
}