using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Cli.Commands.Secret;

public class SecretEditCommand(IStraumrSecretService secretService) : AsyncCommand<SecretEditCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        Guid secretId;
        string tempPath;
        try
        {
            (secretId, tempPath) = await secretService.PrepareEditAsync(settings.Identifier);
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

        try
        {
            int? exitCode = await LaunchEditorAsync(editor, tempPath, cancellation);
            if (exitCode is not null)
            {
                return exitCode.Value;
            }

            string editedJson = await File.ReadAllTextAsync(tempPath, cancellation);
            StraumrSecret? deserialized;
            try
            {
                deserialized = JsonSerializer.Deserialize<StraumrSecret>(editedJson,
                    StraumrJsonContext.Default.StraumrSecret);
            }
            catch (JsonException ex)
            {
                AnsiConsole.MarkupLine($"[red]Invalid secret JSON: {Markup.Escape(ex.Message)}[/]");
                return 1;
            }

            if (deserialized is null)
            {
                AnsiConsole.MarkupLine("[red]Invalid secret JSON.[/]");
                return 1;
            }

            if (deserialized.Id != secretId)
            {
                AnsiConsole.MarkupLine("[red]Secret ID cannot be changed.[/]");
                return 1;
            }

            try
            {
                secretService.ApplyEdit(secretId, tempPath);
                AnsiConsole.MarkupLine($"[green]Updated secret[/] [bold]{deserialized.Name}[/] ({deserialized.Id})");
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
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the secret to edit")]
        public required string Identifier { get; set; }
    }
}
