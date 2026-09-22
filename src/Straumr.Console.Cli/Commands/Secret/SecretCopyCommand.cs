using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Secret;

[UsedImplicitly]
public sealed class SecretCopyCommand(IStraumrSecretService secretService)
    : AsyncCommand<SecretCopyCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SecretCopyCommandSettings settings,
        CancellationToken cancellation)
    {
        try
        {
            StraumrSecret source =
                await GetSecretAsync(secretService, settings.Identifier, true,
                    cancellation);
            StraumrSecret copy = source.CopyAs(settings.NewName);
            await secretService.CreateAsync(copy, cancellation);

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
}
