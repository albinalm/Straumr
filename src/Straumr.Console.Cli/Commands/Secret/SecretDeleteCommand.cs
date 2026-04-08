using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Cli.Commands.Secret;

public class SecretDeleteCommand(IStraumrSecretService secretService) : AsyncCommand<SecretDeleteCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        try
        {
            await secretService.DeleteAsync(settings.Identifier);
            AnsiConsole.MarkupLine($"[green]Deleted secret[/] [bold]{settings.Identifier}[/]");
            return 0;
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

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the secret to delete")]
        public required string Identifier { get; set; }
    }
}
