using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Auth;

public class AuthGetCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService)
    : AsyncCommand<AuthGetCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        StraumrWorkspaceEntry? workspaceEntry = optionsService.Options.CurrentWorkspace;
        if (workspaceEntry is null)
        {
            AnsiConsole.MarkupLine("[red]No workspace loaded. Please load a workspace using 'workspace use <name>'[/]");
            return 1;
        }

        StraumrWorkspace workspace;
        try
        {
            workspace = await workspaceService.PeekWorkspace(workspaceEntry.Path);
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        Guid? foundId = null;

        if (Guid.TryParse(settings.Identifier, out Guid guid) && workspace.Auths.Contains(guid))
        {
            foundId = guid;
        }

        if (foundId is null)
        {
            foreach (Guid id in workspace.Auths)
            {
                try
                {
                    StraumrAuth a = await authService.PeekByIdAsync(id);
                    if (!string.Equals(a.Name, settings.Identifier, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foundId = id;
                    break;
                }
                catch (StraumrException) { }
            }
        }

        if (foundId is null)
        {
            AnsiConsole.MarkupLine(
                $"[red]No auth found with the identifier: {Markup.Escape(settings.Identifier)}[/]");
            return 1;
        }

        if (settings.Json)
        {
            string authPath =
                Path.Combine(Path.GetDirectoryName(workspaceEntry.Path)!, foundId.Value + ".json");
            if (!File.Exists(authPath))
            {
                await System.Console.Error.WriteLineAsync("Auth is missing");
                return 1;
            }

            string json = await File.ReadAllTextAsync(authPath, cancellation);
            System.Console.WriteLine(json);
            return 0;
        }

        StraumrAuth? auth = null;
        string status;
        try
        {
            auth = await authService.PeekByIdAsync(foundId.Value);
            status = "[green]Valid[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.CorruptEntry)
        {
            status = "[red]Corrupt[/]";
        }
        catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
        {
            status = "[yellow]Missing[/]";
        }

        Table table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("Key").NoWrap())
            .AddColumn(new TableColumn("Value"));

        table.AddRow("[grey]ID[/]", Markup.Escape((auth?.Id ?? foundId.Value).ToString()));
        table.AddRow("[grey]Name[/]",
            auth is not null ? $"[bold]{Markup.Escape(auth.Name)}[/]" : "[grey]N/A[/]");
        table.AddRow("[grey]Type[/]", AuthDisplayName(auth?.Config));
        table.AddRow("[grey]Last Accessed[/]",
            auth?.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Modified[/]",
            auth?.Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Status[/]", status);

        string displayName = auth?.Name ?? foundId.Value.ToString();
        Panel panel = new Panel(table)
            .Header($"Auth [bold]{Markup.Escape(displayName)}[/]", Justify.Left)
            .BorderColor(Color.Blue)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        return auth is not null ? 0 : 1;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the auth to get")]
        public required string Identifier { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the auth as raw JSON")]
        public bool Json { get; set; }
    }
}
