using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;

namespace Straumr.Cli.Commands.Auth;

public class AuthGetCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthTemplateService templateService)
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

        if (Guid.TryParse(settings.Identifier, out Guid guid) && workspace.AuthTemplates.Contains(guid))
        {
            foundId = guid;
        }

        if (foundId is null)
        {
            foreach (Guid id in workspace.AuthTemplates)
            {
                try
                {
                    StraumrAuthTemplate t = await templateService.PeekByIdAsync(id);
                    if (!string.Equals(t.Name, settings.Identifier, StringComparison.OrdinalIgnoreCase))
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
                $"[red]No auth template found with the identifier: {Markup.Escape(settings.Identifier)}[/]");
            return 1;
        }

        if (settings.Json)
        {
            string templatePath =
                Path.Combine(Path.GetDirectoryName(workspaceEntry.Path)!, foundId.Value + ".auth.json");
            if (!File.Exists(templatePath))
            {
                await System.Console.Error.WriteLineAsync("Auth template is missing");
                return 1;
            }

            string json = await File.ReadAllTextAsync(templatePath, cancellation);
            System.Console.WriteLine(json);
            return 0;
        }

        StraumrAuthTemplate? template = null;
        string status;
        try
        {
            template = await templateService.PeekByIdAsync(foundId.Value);
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

        table.AddRow("[grey]ID[/]", Markup.Escape((template?.Id ?? foundId.Value).ToString()));
        table.AddRow("[grey]Name[/]",
            template is not null ? $"[bold]{Markup.Escape(template.Name)}[/]" : "[grey]N/A[/]");
        table.AddRow("[grey]Type[/]", AuthDisplayName(template?.Config));
        table.AddRow("[grey]Last Accessed[/]",
            template?.LastAccessed.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Modified[/]",
            template?.Modified.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey]N/A[/]");
        table.AddRow("[grey]Status[/]", status);

        string displayName = template?.Name ?? foundId.Value.ToString();
        Panel panel = new Panel(table)
            .Header($"Auth Template [bold]{Markup.Escape(displayName)}[/]", Justify.Left)
            .BorderColor(Color.Blue)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
        return template is not null ? 0 : 1;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")] public required string Identifier { get; set; }
        [CommandOption("-j|--json")] public bool Json { get; set; }
    }
}