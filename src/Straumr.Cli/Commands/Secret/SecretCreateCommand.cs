using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Cli.Commands.Secret;

public class SecretCreateCommand(IStraumrSecretService secretService) : AsyncCommand<SecretCreateCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
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
            var secret = new StraumrSecret
            {
                Name = name,
                Value = value
            };

            await secretService.CreateAsync(secret);
            AnsiConsole.MarkupLine($"[green]Created secret[/] [bold]{secret.Name}[/] ({secret.Id})");
            return 0;
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason is StraumrError.EntryConflict or StraumrError.MissingEntry ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
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
    }
}
