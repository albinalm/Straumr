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

namespace Straumr.Cli.Commands.Secret;

public class SecretListCommand(
    IStraumrOptionsService optionsService,
    IStraumrSecretService secretService)
    : AsyncCommand<SecretListCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        IEnumerable<StraumrSecretEntry> entries = optionsService.Options.Secrets;

        if (!string.IsNullOrEmpty(settings.Filter))
        {
            // Need to peek names for name-based filtering; filter by ID prefix first, then by name
            var filtered = new List<(StraumrSecretEntry entry, StraumrSecret? secret, string status)>();
            foreach (StraumrSecretEntry entry in entries)
            {
                SecretListEntry secretEntry = await GetSecretAsync(entry.Id);
                bool matchesId = entry.Id.ToString().StartsWith(settings.Filter, StringComparison.OrdinalIgnoreCase);
                bool matchesName = secretEntry.Secret?.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase) == true;
                if (matchesId || matchesName)
                {
                    filtered.Add((entry, secretEntry.Secret, secretEntry.Status));
                }
            }
            return await RenderAsync(settings, filtered.Select(f => (f.entry, f.secret, f.status)).ToArray());
        }

        var all = new List<(StraumrSecretEntry entry, StraumrSecret? secret, string status)>();
        foreach (StraumrSecretEntry entry in entries)
        {
            SecretListEntry secretEntry = await GetSecretAsync(entry.Id);
            all.Add((entry, secretEntry.Secret, secretEntry.Status));
        }

        return await RenderAsync(settings, all.ToArray());
    }

    private static Task<int> RenderAsync(Settings settings,
        (StraumrSecretEntry entry, StraumrSecret? secret, string status)[] items)
    {
        if (settings.Json)
        {
            var jsonItems = items.Select(i => new SecretListItem(
                Id: i.entry.Id.ToString(),
                Name: i.secret?.Name ?? "N/A",
                Status: StripMarkup(i.status)
            )).ToArray();
            System.Console.WriteLine(JsonSerializer.Serialize(jsonItems, CliJsonContext.Default.SecretListItemArray));
            return Task.FromResult(0);
        }

        if (items.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No secrets defined.[/]");
            return Task.FromResult(0);
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Value");
        table.AddColumn("Status");

        foreach ((StraumrSecretEntry entry, StraumrSecret? secret, string status) in items)
        {
            table.AddRow(
                entry.Id.ToString(),
                Markup.Escape(secret?.Name ?? "N/A"),
                Markup.Escape(secret?.Value ?? "N/A"),
                status);
        }

        AnsiConsole.Write(table);
        return Task.FromResult(0);
    }

    private async Task<SecretListEntry> GetSecretAsync(Guid secretId)
    {
        string status;
        StraumrSecret? secret = null;
        try
        {
            secret = await secretService.PeekByIdAsync(secretId);
            status = "[green]Valid[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            status = "[red]Corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            status = "[yellow]Missing[/]";
        }

        return new SecretListEntry
        {
            Secret = secret,
            Status = status
        };
    }

    private static string StripMarkup(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"\[.*?\]", string.Empty);

    private class SecretListEntry
    {
        public StraumrSecret? Secret { get; init; }
        public required string Status { get; init; }
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
