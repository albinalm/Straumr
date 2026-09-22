using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public class SecretGetCommand(
    IStraumrStateService stateService,
    IStraumrSecretService secretService)
    : AsyncCommand<SecretGetCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SecretGetCommandSettings settings,
        CancellationToken cancellation)
    {
        Guid? foundId = null;

        if (Guid.TryParse(settings.Identifier, out Guid guid) && stateService.State.Secrets.Any(x => x.Id == guid))
        {
            foundId = guid;
        }

        if (foundId is null)
        {
            foreach (StraumrSecretEntry entry in stateService.State.Secrets.Where(entry => File.Exists(entry.Path)))
            {
                try
                {
                    StraumrSecret candidate = await secretService.GetAsync(entry.Id,
                        cancellationToken: cancellation);
                    if (!string.Equals(candidate.Name, settings.Identifier, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foundId = entry.Id;
                    break;
                }
                catch (StraumrException) { }
            }
        }

        if (foundId is null)
        {
            AnsiConsole.MarkupLine(
                $"[red]No secret found with the identifier: {Markup.Escape(settings.Identifier)}[/]");
            return 1;
        }

        if (settings.Json)
        {
            try
            {
                StraumrSecret jsonSecret = await secretService.GetAsync(foundId.Value,
                    cancellationToken: cancellation);
                System.Console.WriteLine(JsonSerializer.Serialize(jsonSecret, StraumrJsonContext.Default.StraumrSecret));
                return 0;
            }
            catch (StraumrException ex)
            {
                WriteError(ex.Message, settings.Json);
                return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
            }
        }

        StraumrSecret? secret = null;
        string status;
        try
        {
            secret = await secretService.GetAsync(foundId.Value, cancellationToken: cancellation);
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

        Table table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Key").NoWrap())
            .AddColumn(new TableColumn("Value"));

        table.AddRow("[grey]ID[/]", Markup.Escape((secret?.Id ?? foundId.Value).ToString()));
        table.AddRow("[grey]Name[/]", secret is not null ? $"[bold]{Markup.Escape(secret.Name)}[/]" : "[grey]N/A[/]");
        table.AddRow("[grey]Value[/]", secret is not null ? Markup.Escape(secret.Value) : "[grey]N/A[/]");
        table.AddRow("[grey]Last Accessed[/]",
            secret?.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Modified[/]",
            secret?.Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Status[/]", status);

        string displayName = secret?.Name ?? foundId.Value.ToString();
        Panel panel = new Panel(table)
            .Header($"Secret [bold]{Markup.Escape(displayName)}[/]", Justify.Left)
            .BorderColor(Color.Blue)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        return secret is not null ? 0 : 1;
    }
}
