using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Infrastructure;
using Straumr.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Auth;

public class AuthListCommand(IStraumrOptionsService optionsService, IStraumrAuthService authService)
    : AsyncCommand<AuthListCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        bool hasWorkspace = optionsService.Options.CurrentWorkspace != null;

        if (!hasWorkspace)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        IReadOnlyList<StraumrAuth> auths;
        try
        {
            auths = await authService.ListAsync();
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason == StraumrError.MissingEntry ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
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
            var items = filteredList.Select(a => new AuthListItem(
                Id: a.Id.ToString(),
                Name: a.Name,
                Type: AuthTypeName(a.Config)
            )).ToArray();
            System.Console.WriteLine(JsonSerializer.Serialize(items, CliJsonContext.Default.AuthListItemArray));
            return 0;
        }

        if (filteredList.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No auths defined.[/]");
            return 0;
        }

        var table = new Table();
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
    }
}
