using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public class SecretCreateCommand(IStraumrSecretService secretService) : AsyncCommand<SecretCreateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SecretCreateCommandSettings settings,
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
}
