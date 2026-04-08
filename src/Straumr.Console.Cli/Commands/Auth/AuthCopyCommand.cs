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
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Auth;

public class AuthCopyCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService)
    : AsyncCommand<AuthCopyCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, optionsService, workspaceService);
            if (resolved is null)
            {
                WriteError($"Workspace not found: {settings.Workspace}", settings.Json);
                return 1;
            }

            optionsService.Options.CurrentWorkspace = resolved;
        }

        if (optionsService.Options.CurrentWorkspace is null)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        try
        {
            StraumrAuth copy = await authService.CopyAsync(settings.Identifier, settings.NewName);

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

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Identifier>")]
        [Description("Name or ID of the auth to copy")]
        public required string Identifier { get; set; }

        [CommandArgument(1, "<NewName>")]
        [Description("Name for the new auth")]
        public required string NewName { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the result as JSON")]
        public bool Json { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }
}
