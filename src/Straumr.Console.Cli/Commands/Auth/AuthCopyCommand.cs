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
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public class AuthCopyCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService)
    : AsyncCommand<AuthCopyCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AuthCopyCommandSettings settings,
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
            StraumrAuth source =
                await GetAuthAsync(authService, workspaceEntry, settings.Identifier,
                    cancellationToken: cancellation);
            StraumrAuth copy = source.CopyAs(settings.NewName);
            await authService.CreateAsync(workspaceEntry, copy, cancellation);

            if (settings.Json)
            {
                var result = new AuthListItem(copy.Id.ToString(), copy.Name, AuthTypeName(copy.Config));
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.AuthListItem));
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $"[green]Copied auth[/] [bold]{Markup.Escape(settings.Identifier)}[/] to [bold]{Markup.Escape(settings.NewName)}[/]");
            }

            return 0;
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
}
