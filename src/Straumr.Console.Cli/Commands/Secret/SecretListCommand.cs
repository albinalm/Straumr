using System.Text.Json;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public class SecretListCommand(
    IStraumrStateService stateService,
    IStraumrSecretService secretService)
    : AsyncCommand<SecretListCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SecretListCommandSettings settings,
        CancellationToken cancellation)
    {
        IEnumerable<StraumrSecretEntry> entries = stateService.State.Secrets;

        if (!string.IsNullOrEmpty(settings.Filter))
        {
            List<(StraumrSecretEntry entry, StraumrSecret? secret, string status)> filtered = new();
            foreach (StraumrSecretEntry entry in entries)
            {
                SecretListEntryModel secretEntry = await GetSecretAsync(entry.Id);
                bool matchesId = entry.Id.ToString().StartsWith(settings.Filter, StringComparison.OrdinalIgnoreCase);
                bool matchesName = secretEntry.Secret?.Name.Contains(settings.Filter, StringComparison.OrdinalIgnoreCase) == true;
                if (matchesId || matchesName)
                {
                    filtered.Add((entry, secretEntry.Secret, secretEntry.Status));
                }
            }
            return await RenderAsync(settings, filtered.Select(f => (f.entry, f.secret, f.status)).ToArray());
        }

        List<(StraumrSecretEntry entry, StraumrSecret? secret, string status)> all = new();
        foreach (StraumrSecretEntry entry in entries)
        {
            SecretListEntryModel secretEntry = await GetSecretAsync(entry.Id);
            all.Add((entry, secretEntry.Secret, secretEntry.Status));
        }

        return await RenderAsync(settings, all.ToArray());
    }

    private static Task<int> RenderAsync(SecretListCommandSettings settings,
        (StraumrSecretEntry entry, StraumrSecret? secret, string status)[] items)
    {
        if (settings.Json)
        {
            SecretListItem[] jsonItems = items.Select(i => new SecretListItem(
                i.entry.Id.ToString(),
                i.secret?.Name ?? "N/A",
                StripMarkup(i.status)
            )).ToArray();
            System.Console.WriteLine(JsonSerializer.Serialize(jsonItems, CliJsonContext.Relaxed.SecretListItemArray));
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

    private async Task<SecretListEntryModel> GetSecretAsync(Guid secretId)
    {
        string status;
        StraumrSecret? secret = null;
        try
        {
            secret = await secretService.GetAsync(secretId);
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

        return new SecretListEntryModel
        {
            Secret = secret,
            Status = status
        };
    }

    private static string StripMarkup(string value) =>
        Regex.Replace(value, @"\[.*?\]", string.Empty);
}
