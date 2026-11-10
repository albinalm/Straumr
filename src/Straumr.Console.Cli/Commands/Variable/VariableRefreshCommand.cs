using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public sealed class VariableRefreshCommand(IStraumrStateService stateService, IStraumrWorkspaceService workspaceService,
    IStraumrVariableService variableService, IStraumrRequestService requestService) : AsyncCommand<VariableRefreshCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VariableRefreshCommandSettings settings, CancellationToken cancellation)
    {
        try
        {
            StraumrWorkspaceEntry? workspace = settings.Workspace is { } identifier
                ? await ResolveWorkspaceEntryAsync(identifier, workspaceService) : stateService.State.CurrentWorkspace;
            if (workspace is null)
            {
                WriteError("Workspace not found; select a workspace or pass --workspace", settings.Json);
                return 1;
            }
            StraumrVariable variable = await GetVariableAsync(variableService, workspace, settings.Identifier, cancellationToken: cancellation);
            variable = await requestService.RefreshVariableAsync(workspace, variable.Id, cancellation);
            if (settings.Json)
            {
                var result = new VariableRefreshResult(variable.Id.ToString(), variable.Name, variable.CachedAt);
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.VariableRefreshResult));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Refreshed variable[/] [bold]{Markup.Escape(variable.Name)}[/] ({variable.Id})");
            }
            return 0;
        }
        catch (StraumrException exception)
        {
            WriteError(exception.Message, settings.Json);
            return 1;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WriteError(exception.Message, settings.Json);
            return -1;
        }
    }
}
