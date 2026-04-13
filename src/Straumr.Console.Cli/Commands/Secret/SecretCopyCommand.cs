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
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Secret;

public sealed class SecretCopyCommand(IStraumrSecretService secretService)
    : AsyncCommand<SecretCopyCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        try
        {
            StraumrSecret copy = await secretService.CopyAsync(settings.Identifier, settings.NewName);

            if (settings.Json)
            {
                SecretListItem result = new(copy.Id.ToString(), copy.Name, "Valid");
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.SecretListItem));
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $"[green]Copied secret[/] [bold]{Markup.Escape(settings.Identifier)}[/] to [bold]{Markup.Escape(settings.NewName)}[/]");
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
        [Description("Name or ID of the secret to copy")]
        public required string Identifier { get; set; }

        [CommandArgument(1, "<NewName>")]
        [Description("Name for the new secret")]
        public required string NewName { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the result as JSON")]
        public bool Json { get; set; }
    }
}
