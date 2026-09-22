using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public class WorkspaceDeleteCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceDeleteCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WorkspaceDeleteCommandSettings settings,
        CancellationToken cancellation)
    {
        try
        {
            if (Guid.TryParse(settings.Identifier, out Guid id))
            {
                try
                {
                    await workspaceService.DeleteAsync(id, cancellation);
                    return Success(settings);
                }
                catch (StraumrException exception) when (exception.Reason == StraumrError.EntryNotFound)
                {
                }
            }

            StraumrWorkspace workspace = await workspaceService.GetAsync(
                settings.Identifier, cancellationToken: cancellation);
            await workspaceService.DeleteAsync(workspace.Id, cancellation);
            return Success(settings);
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
    }

    private static int Success(WorkspaceDeleteCommandSettings settings)
    {
        if (!settings.Json)
        {
            AnsiConsole.MarkupLine($"[green]Deleted workspace[/] [bold]{settings.Identifier}[/]");
        }

        return 0;
    }
}
