using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public class AuthGetCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService)
    : AsyncCommand<AuthGetCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AuthGetCommandSettings settings,
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

        if (workspaceEntry is null)
        {
            WriteError("No workspace loaded. Please load a workspace using 'workspace use <name>'", settings.Json);
            return 1;
        }

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.GetAsync(workspaceEntry.Id, cancellationToken: cancellation);
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, settings.Json);
            return 1;
        }

        Guid? foundId = null;

        if (Guid.TryParse(settings.Identifier, out Guid guid) && workspace.Auths.Contains(guid))
        {
            foundId = guid;
        }

        if (foundId is null)
        {
            foreach (Guid id in workspace.Auths)
            {
                try
                {
                    StraumrAuth a = await authService.GetAsync(workspaceEntry, id,
                        cancellationToken: cancellation);
                    if (!string.Equals(a.Name, settings.Identifier, StringComparison.OrdinalIgnoreCase))
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
            WriteError($"No auth found with the identifier: {settings.Identifier}", settings.Json);
            return 1;
        }

        if (settings.Json)
        {
            try
            {
                StraumrAuth jsonAuth = await authService.GetAsync(workspaceEntry, foundId.Value,
                    cancellationToken: cancellation);
                System.Console.WriteLine(JsonSerializer.Serialize(jsonAuth,
                    StraumrJsonContext.Default.StraumrAuth));
                return 0;
            }
            catch (StraumrException ex)
            {
                WriteError(ex.Message, settings.Json);
                return 1;
            }
        }

        StraumrAuth? auth = null;
        string status;
        try
        {
            auth = await authService.GetAsync(workspaceEntry, foundId.Value,
                cancellationToken: cancellation);
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

        table.AddRow("[grey]ID[/]", Markup.Escape((auth?.Id ?? foundId.Value).ToString()));
        table.AddRow("[grey]Name[/]",
            auth is not null ? $"[bold]{Markup.Escape(auth.Name)}[/]" : "[grey]N/A[/]");
        table.AddRow("[grey]Type[/]", AuthDisplayName(auth?.Config));
        table.AddRow("[grey]Last Accessed[/]",
            auth?.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Modified[/]",
            auth?.Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Status[/]", status);

        string displayName = auth?.Name ?? foundId.Value.ToString();
        Panel panel = new Panel(table)
            .Header($"Auth [bold]{Markup.Escape(displayName)}[/]", Justify.Left)
            .BorderColor(Color.Blue)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        return auth is not null ? 0 : 1;
    }
}
