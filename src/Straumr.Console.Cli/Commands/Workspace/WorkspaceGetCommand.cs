using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Workspace;

public class WorkspaceGetCommand(IStraumrOptionsService optionsService, IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceGetCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? entry = null;

        if (Guid.TryParse(settings.Identifier, out Guid guid))
        {
            entry = optionsService.Options.Workspaces.FirstOrDefault(x => x.Id == guid);
        }

        if (entry is null)
        {
            foreach (StraumrWorkspaceEntry candidate in optionsService.Options.Workspaces)
            {
                try
                {
                    StraumrWorkspace w = await workspaceService.PeekWorkspace(candidate.Path);
                    if (!string.Equals(w.Name, settings.Identifier, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    entry = candidate;
                    break;
                }
                catch (StraumrException) { }
            }
        }

        if (entry is null)
        {
            AnsiConsole.MarkupLine(
                $"[red]No workspace found with the identifier: {Markup.Escape(settings.Identifier)}[/]");
            return 1;
        }

        if (settings.Json)
        {
            try
            {
                StraumrWorkspace jsonWorkspace = await workspaceService.PeekWorkspace(entry.Path);
                System.Console.WriteLine(JsonSerializer.Serialize(jsonWorkspace, StraumrJsonContext.Default.StraumrWorkspace));
                return 0;
            }
            catch (StraumrException ex)
            {
                WriteError(ex.Message, settings.Json);
                return 1;
            }
        }

        StraumrWorkspace? workspace = null;
        string status;
        try
        {
            workspace = await workspaceService.PeekWorkspace(entry.Path);
            bool isCurrent = optionsService.Options.CurrentWorkspace?.Id == workspace.Id;
            status = isCurrent ? "[blue]Current[/]" : "[green]Valid[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            status = "[red]Corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            status = "[yellow]Missing[/]";
        }

        Table table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Key").NoWrap())
            .AddColumn(new TableColumn("Value"));

        table.AddRow("[grey]ID[/]", Markup.Escape((workspace?.Id ?? entry.Id).ToString()));
        table.AddRow("[grey]Name[/]",
            workspace is not null ? $"[bold]{Markup.Escape(workspace.Name)}[/]" : "[grey]N/A[/]");
        table.AddRow("[grey]Path[/]", Markup.Escape(entry.Path));
        table.AddRow("[grey]Status[/]", status);
        table.AddRow("[grey]Last Accessed[/]",
            workspace?.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Modified[/]",
            workspace?.Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Requests[/]", workspace?.Requests.Count.ToString() ?? "[grey]N/A[/]");
        table.AddRow("[grey]Auths[/]", workspace?.Auths.Count.ToString() ?? "[grey]N/A[/]");

        string displayName = workspace?.Name ?? entry.Id.ToString();
        Panel panel = new Panel(table)
            .Header($"Workspace [bold]{Markup.Escape(displayName)}[/]", Justify.Left)
            .BorderColor(Color.Blue)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        return workspace is not null ? 0 : 1;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the workspace to get")]
        public required string Identifier { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the workspace as raw JSON")]
        public bool Json { get; set; }
    }
}
