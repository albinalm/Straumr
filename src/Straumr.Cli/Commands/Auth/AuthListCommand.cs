using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Auth;

public class AuthListCommand(IStraumrOptionsService optionsService, IStraumrAuthService authService)
    : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellation)
    {
        bool hasWorkspace = optionsService.Options.CurrentWorkspace != null;

        if (!hasWorkspace)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        IReadOnlyList<StraumrAuth> auths;
        try
        {
            auths = await authService.ListAsync();
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

        if (auths.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No auths defined.[/]");
            return 0;
        }

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("ID");
        table.AddColumn("Type");

        foreach (StraumrAuth auth in auths)
        {
            table.AddRow(
                Markup.Escape(auth.Name),
                auth.Id.ToString(),
                AuthDisplayName(auth.Config));
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
