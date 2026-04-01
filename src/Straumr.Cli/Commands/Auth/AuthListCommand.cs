using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;

namespace Straumr.Cli.Commands.Auth;

public class AuthListCommand(IStraumrAuthTemplateService templateService) : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellation)
    {
        IReadOnlyList<StraumrAuthTemplate> templates;
        try
        {
            templates = await templateService.ListAsync();
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason == StraumrError.MissingEntry ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return -1;
        }

        if (templates.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No auth presets defined.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("ID");
        table.AddColumn("Type");

        foreach (StraumrAuthTemplate template in templates)
        {
            table.AddRow(
                Markup.Escape(template.Name),
                template.Id.ToString(),
                AuthDisplayName(template.Config));
        }

        AnsiConsole.Write(table);
        return 0;
    }
}