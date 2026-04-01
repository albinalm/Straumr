using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Secret;

public class SecretListCommand(
    IStraumrOptionsService optionsService,
    IStraumrSecretService secretService)
    : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellation)
    {
        if (optionsService.Options.Secrets.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No secrets defined.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Value");
        table.AddColumn("Status");

        foreach (StraumrSecretEntry entry in optionsService.Options.Secrets)
        {
            SecretListEntry secretEntry = await GetSecretAsync(entry.Id);
            table.AddRow(
                entry.Id.ToString(),
                Markup.Escape(secretEntry.Secret?.Name ?? "N/A"),
                Markup.Escape(secretEntry.Secret?.Value ?? "N/A"),
                secretEntry.Status);
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private async Task<SecretListEntry> GetSecretAsync(Guid secretId)
    {
        string status;
        StraumrSecret? secret = null;
        try
        {
            secret = await secretService.GetAsync(secretId.ToString());
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

    private class SecretListEntry
    {
        public StraumrSecret? Secret { get; init; }
        public required string Status { get; init; }
    }
}
