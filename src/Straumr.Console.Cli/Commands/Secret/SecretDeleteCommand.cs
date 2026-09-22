using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public class SecretDeleteCommand(IStraumrSecretService secretService) : AsyncCommand<SecretDeleteCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SecretDeleteCommandSettings settings,
        CancellationToken cancellation)
    {
        try
        {
            if (Guid.TryParse(settings.Identifier, out Guid id))
            {
                try
                {
                    await secretService.DeleteAsync(id, cancellation);
                    return Success(settings.Identifier);
                }
                catch (StraumrException exception) when (exception.Reason == StraumrError.EntryNotFound)
                {
                }
            }

            StraumrSecret secret = await secretService.GetAsync(
                settings.Identifier, cancellationToken: cancellation);
            await secretService.DeleteAsync(secret.Id, cancellation);
            return Success(settings.Identifier);
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return -1;
        }
    }

    private static int Success(string identifier)
    {
        AnsiConsole.MarkupLine($"[green]Deleted secret[/] [bold]{identifier}[/]");
        return 0;
    }
}
