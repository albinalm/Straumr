using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.ConsoleHelpers;

namespace Straumr.Cli.Commands.Secret;

public class SecretGetCommand(
    IStraumrOptionsService optionsService,
    IStraumrSecretService secretService)
    : AsyncCommand<SecretGetCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        Guid? foundId = null;

        if (Guid.TryParse(settings.Identifier, out Guid guid) && optionsService.Options.Secrets.Any(x => x.Id == guid))
        {
            foundId = guid;
        }

        if (foundId is null)
        {
            foreach (StraumrSecretEntry entry in optionsService.Options.Secrets.Where(entry => File.Exists(entry.Path)))
            {
                try
                {
                    StraumrSecret candidate = await secretService.PeekByIdAsync(entry.Id);
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
                StraumrSecret jsonSecret = await secretService.PeekByIdAsync(foundId.Value);
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
            secret = await secretService.PeekByIdAsync(foundId.Value);
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

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the secret to get")]
        public required string Identifier { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the secret as raw JSON")]
        public bool Json { get; set; }
    }
}
