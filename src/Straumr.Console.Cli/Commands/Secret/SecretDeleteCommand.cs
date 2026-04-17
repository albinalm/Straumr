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

public class SecretDeleteCommand(IStraumrSecretService secretService) : AsyncCommand<SecretDeleteCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        try
        {
            StraumrSecret secret = await secretService.GetAsync(settings.Identifier);
            await secretService.DeleteAsync(settings.Identifier);

            if (settings.Json)
            {
                SecretDeleteResult result = new(secret.Id.ToString(), secret.Name);
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.SecretDeleteResult));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Deleted secret[/] [bold]{settings.Identifier}[/]");
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
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the secret to delete")]
        public required string Identifier { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the deleted secret as JSON; errors are emitted as JSON to stderr")]
        public bool Json { get; set; }
    }
}
