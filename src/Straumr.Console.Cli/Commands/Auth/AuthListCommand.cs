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
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Auth;

public class AuthListCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService)
    : AsyncCommand<AuthListCommand.Settings>
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

        bool hasWorkspace = optionsService.Options.CurrentWorkspace != null;

        if (!hasWorkspace)
        {
            WriteError("No workspace loaded. Please load a workspace using 'workspace use <name>'", settings.Json);
            return 1;
        }

        IReadOnlyList<StraumrAuth> auths;
        try
        {
            auths = await authService.ListAsync();
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

        IEnumerable<StraumrAuth> filtered = auths;
        if (!string.IsNullOrEmpty(settings.Filter))
        {
            filtered = auths.Where(a =>
                a.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase) ||
                a.Id.ToString().StartsWith(settings.Filter, StringComparison.OrdinalIgnoreCase));
        }

        StraumrAuth[] filteredList = filtered.ToArray();

        if (settings.Json)
        {
            AuthListItem[] items = filteredList.Select(a => new AuthListItem(
                Id: a.Id.ToString(),
                Name: a.Name,
                Type: AuthTypeName(a.Config)
            )).ToArray();
            System.Console.WriteLine(JsonSerializer.Serialize(items, CliJsonContext.Relaxed.AuthListItemArray));
            return 0;
        }

        if (filteredList.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No auths defined.[/]");
            return 0;
        }

        Table table = new Table();
        table.AddColumn("Name");
        table.AddColumn("ID");
        table.AddColumn("Type");

        foreach (StraumrAuth auth in filteredList)
        {
            table.AddRow(
                Markup.Escape(auth.Name),
                auth.Id.ToString(),
                AuthDisplayName(auth.Config));
        }

        AnsiConsole.Write(table);
        return 0;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-j|--json")]
        [Description("Output as JSON array")]
        public bool Json { get; set; }

        [CommandOption("--filter")]
        [Description("Filter results by name (substring) or ID prefix")]
        public string? Filter { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }
}