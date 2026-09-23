using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public class VariableGetCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrVariableService variableService)
    : AsyncCommand<VariableGetCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VariableGetCommandSettings settings,
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

        StraumrVariable variable;
        try
        {
            variable = await GetVariableAsync(
                variableService, workspaceEntry, settings.Identifier, cancellationToken: cancellation);
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, settings.Json);
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, settings.Json);
            return -1;
        }

        if (settings.Json)
        {
            System.Console.WriteLine(JsonSerializer.Serialize(variable, StraumrJsonContext.Default.StraumrVariable));
            return 0;
        }

        Table table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Key").NoWrap())
            .AddColumn(new TableColumn("Value"));

        table.AddRow("[grey]ID[/]", Markup.Escape(variable.Id.ToString()));
        table.AddRow("[grey]Name[/]", $"[bold]{Markup.Escape(variable.Name)}[/]");
        table.AddRow("[grey]Value[/]", Markup.Escape(variable.Value));
        table.AddRow("[grey]Placeholder[/]", Markup.Escape($"{{{{{variable.Name}}}}}"));
        table.AddRow("[grey]Last Accessed[/]", variable.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
        table.AddRow("[grey]Modified[/]", variable.Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"));

        Panel panel = new Panel(table)
            .Header($"Variable [bold]{Markup.Escape(variable.Name)}[/]", Justify.Left)
            .BorderColor(Color.Blue)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        return 0;
    }
}
