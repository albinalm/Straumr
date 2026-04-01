using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Cli.Helpers.HttpCommandHelpers;

namespace Straumr.Cli.Commands.Request;

public class RequestGetCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService)
    : AsyncCommand<RequestGetCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? workspaceEntry = optionsService.Options.CurrentWorkspace;
        if (workspaceEntry is null)
        {
            AnsiConsole.MarkupLine("[red]No workspace loaded. Please load a workspace using 'workspace use <name>'[/]");
            return 1;
        }

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.PeekWorkspace(workspaceEntry.Path);
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        Guid? foundId = null;

        if (Guid.TryParse(settings.Identifier, out Guid guid) && workspace.Requests.Contains(guid))
        {
            foundId = guid;
        }

        if (foundId is null)
        {
            foreach (Guid id in workspace.Requests)
            {
                try
                {
                    StraumrRequest r = await requestService.PeekByIdAsync(id);
                    if (!string.Equals(r.Name, settings.Identifier, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foundId = id;
                    break;
                }
                catch (StraumrException) { }
            }
        }

        if (foundId is null)
        {
            AnsiConsole.MarkupLine(
                $"[red]No request found with the identifier: {Markup.Escape(settings.Identifier)}[/]");
            return 1;
        }

        if (settings.Json)
        {
            string requestPath = Path.Combine(Path.GetDirectoryName(workspaceEntry.Path)!, foundId.Value + ".json");
            if (!File.Exists(requestPath))
            {
                await System.Console.Error.WriteLineAsync("Request is missing");
                return 1;
            }

            string json = await File.ReadAllTextAsync(requestPath, cancellation);
            System.Console.WriteLine(json);
            return 0;
        }

        StraumrRequest? request = null;
        string status;
        try
        {
            request = await requestService.PeekByIdAsync(foundId.Value);
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

        Table table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Key").NoWrap())
            .AddColumn(new TableColumn("Value"));

        table.AddRow("[grey]ID[/]", Markup.Escape((request?.Id ?? foundId.Value).ToString()));
        table.AddRow("[grey]Name[/]", request is not null ? $"[bold]{Markup.Escape(request.Name)}[/]" : "[grey]N/A[/]");
        table.AddRow("[grey]Method[/]",
            request is not null ? $"[blue]{Markup.Escape(request.Method.ToString())}[/]" : "[grey]N/A[/]");
        table.AddRow("[grey]URI[/]", request is not null ? Markup.Escape(request.Uri) : "[grey]N/A[/]");
        table.AddRow("[grey]Auth[/]", AuthDisplayName(request?.Auth));
        table.AddRow("[grey]Headers[/]", request?.Headers.Count.ToString() ?? "[grey]N/A[/]");
        table.AddRow("[grey]Params[/]", request?.Params.Count.ToString() ?? "[grey]N/A[/]");
        table.AddRow("[grey]Body[/]", request is not null ? BodyTypeDisplayName(request.BodyType) : "[grey]N/A[/]");
        table.AddRow("[grey]Last Accessed[/]",
            request?.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Modified[/]",
            request?.Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Status[/]", status);

        string displayName = request?.Name ?? foundId.Value.ToString();
        Panel panel = new Panel(table)
            .Header($"Request [bold]{Markup.Escape(displayName)}[/]", Justify.Left)
            .BorderColor(Color.Blue)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        return request is not null ? 0 : 1;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")] public required string Identifier { get; set; }
        [CommandOption("-j|--json")] public bool Json { get; set; }
    }
}