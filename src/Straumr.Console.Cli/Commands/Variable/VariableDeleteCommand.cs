using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public class VariableDeleteCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrVariableService variableService) : AsyncCommand<VariableDeleteCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VariableDeleteCommandSettings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? workspaceEntry = stateService.State.CurrentWorkspace;

        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, workspaceService);
            if (resolved is null)
            {
                AnsiConsole.MarkupLine($"[red]Workspace not found: {Markup.Escape(settings.Workspace)}[/]");
                return 1;
            }

            workspaceEntry = resolved;
        }

        if (workspaceEntry is null)
        {
            AnsiConsole.MarkupLine("[red]No workspace loaded. Please load a workspace using 'workspace use <name>'[/]");
            return 1;
        }

        try
        {
            StraumrVariable variable = await GetVariableAsync(
                variableService, workspaceEntry, settings.Identifier, cancellationToken: cancellation);
            await variableService.DeleteAsync(workspaceEntry, variable.Id, cancellation);
            AnsiConsole.MarkupLine($"[green]Deleted variable[/] [bold]{Markup.Escape(settings.Identifier)}[/]");
            return 0;
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return -1;
        }
    }
}
