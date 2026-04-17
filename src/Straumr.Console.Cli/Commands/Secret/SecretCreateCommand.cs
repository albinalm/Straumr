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

public class SecretCreateCommand(IStraumrSecretService secretService) : AsyncCommand<SecretCreateCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (settings.Json && (string.IsNullOrWhiteSpace(settings.Name) || settings.Value is null))
        {
            WriteError("Name and value are required when using --json.", true);
            return 1;
        }

        string name = settings.Name ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Name:")
                .Validate(v => string.IsNullOrWhiteSpace(v)
                    ? ValidationResult.Error("Name cannot be empty.")
                    : ValidationResult.Success()));

        string value = settings.Value ?? AnsiConsole.Prompt(
            new TextPrompt<string>("Value:")
                .Secret());

        try
        {
            StraumrSecret secret = new StraumrSecret
            {
                Name = name,
                Value = value
            };

            await secretService.CreateAsync(secret);

            if (settings.Json)
            {
                SecretListItem result = new(secret.Id.ToString(), secret.Name, "Valid");
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.SecretListItem));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Created secret[/] [bold]{secret.Name}[/] ({secret.Id})");
            }

            return 0;
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, settings.Json);
            return ex.Reason is StraumrError.EntryConflict or StraumrError.MissingEntry ? 1 : -1;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, settings.Json);
            return -1;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[Name]")]
        [Description("Name of the secret to create")]
        public string? Name { get; set; }

        [CommandArgument(1, "[Value]")]
        [Description("Value of the secret to create")]
        public string? Value { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the created secret as JSON; requires both name and value")]
        public bool Json { get; set; }
    }
}
