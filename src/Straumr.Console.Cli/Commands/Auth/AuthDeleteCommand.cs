using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public class AuthDeleteCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService)
    : AsyncCommand<AuthDeleteCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AuthDeleteCommandSettings settings,
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
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        try
        {
            if (Guid.TryParse(settings.Identifier, out Guid id))
            {
                try
                {
                    await authService.DeleteAsync(workspaceEntry, id, cancellation);
                    return Success(settings);
                }
                catch (StraumrException exception) when (exception.Reason == StraumrError.EntryNotFound)
                {
                }
            }

            StraumrAuth auth = await authService.GetAsync(
                workspaceEntry, settings.Identifier, cancellationToken: cancellation);
            await authService.DeleteAsync(workspaceEntry, auth.Id, cancellation);
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

    private static int Success(AuthDeleteCommandSettings settings)
    {
        if (!settings.Json)
        {
            AnsiConsole.MarkupLine($"[green]Deleted auth[/] [bold]{settings.Identifier}[/]");
        }

        return 0;
    }
}
