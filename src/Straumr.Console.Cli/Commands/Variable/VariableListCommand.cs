using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public class VariableListCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrVariableService variableService)
    : AsyncCommand<VariableListCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VariableListCommandSettings settings,
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

        IReadOnlyList<StraumrVariable> variables;
        try
        {
            variables = await variableService.ListAsync(workspaceEntry, cancellation);
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, settings.Json);
            return ex.Reason == StraumrError.MissingEntry ? 1 : -1;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, settings.Json);
            return -1;
        }

        IEnumerable<StraumrVariable> filtered = variables;
        if (!string.IsNullOrEmpty(settings.Filter))
        {
            filtered = variables.Where(variable =>
                variable.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase) ||
                variable.Id.ToString().StartsWith(settings.Filter, StringComparison.OrdinalIgnoreCase));
        }

        StraumrVariable[] items = filtered.ToArray();

        if (settings.Json)
        {
            VariableListItem[] jsonItems = items.Select(variable => new VariableListItem(
                variable.Id.ToString(),
                variable.Name,
                "Valid"
            )).ToArray();
            System.Console.WriteLine(JsonSerializer.Serialize(jsonItems, CliJsonContext.Relaxed.VariableListItemArray));
            return 0;
        }

        if (items.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No variables defined.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Value");

        foreach (StraumrVariable variable in items)
        {
            table.AddRow(
                variable.Id.ToString(),
                Markup.Escape(variable.Name),
                Markup.Escape(variable.Value));
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
