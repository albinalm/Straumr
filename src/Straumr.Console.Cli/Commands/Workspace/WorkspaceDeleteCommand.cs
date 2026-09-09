using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Workspace;

public class WorkspaceDeleteCommand(IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceDeleteCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
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

            Straumr.Core.Models.StraumrWorkspace workspace = await workspaceService.GetAsync(
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

    private static int Success(Settings settings)
    {
        if (!settings.Json)
        {
            AnsiConsole.MarkupLine($"[green]Deleted workspace[/] [bold]{settings.Identifier}[/]");
        }

        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the workspace to delete")]
        public required string Identifier { get; set; }

        [CommandOption("-j|--json")]
        [Description("Suppress human-readable output; errors are emitted as JSON to stderr")]
        public bool Json { get; set; }
    }
}
