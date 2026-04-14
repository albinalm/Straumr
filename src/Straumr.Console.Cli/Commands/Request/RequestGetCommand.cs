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
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Helpers.HttpCommandHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Request;

public class RequestGetCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService)
    : AsyncCommand<RequestGetCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? workspaceEntry = optionsService.Options.CurrentWorkspace;

        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, optionsService, workspaceService);
            if (resolved is null)
            {
                WriteError($"Workspace not found: {settings.Workspace}", settings.Json);
                return 1;
            }

            workspaceEntry = resolved;
        }

        if (workspaceEntry is null)
        {
            WriteError("No workspace loaded. Please load a workspace using 'workspace use <name>'", settings.Json);
            return 1;
        }

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.PeekWorkspaceAsync(workspaceEntry.Path);
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, settings.Json);
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
                    StraumrRequest r = await requestService.PeekByIdAsync(id, workspaceEntry);
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
            WriteError($"No request found with the identifier: {settings.Identifier}", settings.Json);
            return 1;
        }

        if (settings.Json)
        {
            StraumrRequest req;
            try
            {
                req = await requestService.PeekByIdAsync(foundId.Value, workspaceEntry);
            }
            catch (StraumrException ex)
            {
                WriteError(ex.Message, settings.Json);
                return 1;
            }

            string? currentBody = req.Bodies.TryGetValue(req.BodyType, out string? b) ? b : null;
            RequestGetResult result = new RequestGetResult(
                Id: req.Id.ToString(),
                Name: req.Name,
                Method: req.Method.Method,
                Uri: req.Uri,
                BodyType: req.BodyType.ToString(),
                Headers: req.Headers,
                Params: req.Params,
                Body: currentBody,
                AuthId: req.AuthId?.ToString(),
                LastAccessed: req.LastAccessed.LocalDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                Modified: req.Modified.LocalDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
            );
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.RequestGetResult));
            return 0;
        }

        StraumrRequest? request = null;
        string? resolvedUrl = null;
        IReadOnlyList<string> warnings = [];
        string status;
        try
        {
            request = await requestService.PeekByIdAsync(foundId.Value, workspaceEntry);
            (resolvedUrl, warnings) = await requestService.ResolveUrlAsync(request);
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

        IReadOnlyList<StraumrAuth> auths = [];
        if (request?.AuthId is not null)
        {
            try
            {
                auths = await authService.ListAsync(workspaceEntry);
            }
            catch (StraumrException) { }
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
        table.AddRow("[grey]URI[/]", request is not null ? Markup.Escape(resolvedUrl ?? request.Uri) : "[grey]N/A[/]");
        table.AddRow("[grey]Auth[/]", AuthDisplayName(request?.AuthId, auths));
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

        foreach (string warning in warnings)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Warning:[/] {Markup.Escape(warning)}");
        }

        return request is not null ? 0 : 1;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the request to get")]
        public required string Identifier { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the request as JSON")]
        public bool Json { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }
}
