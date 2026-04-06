using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Infrastructure;
using Straumr.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;
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
        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, optionsService, workspaceService);
            if (resolved is null)
            {
                if (settings.Json)
                    await System.Console.Error.WriteLineAsync(
                        $"{{\"error\":{{\"message\":\"Workspace not found: {settings.Workspace}\"}}}}");
                else
                    AnsiConsole.MarkupLine($"[red]Workspace not found: {Markup.Escape(settings.Workspace)}[/]");
                return 1;
            }

            optionsService.Options.CurrentWorkspace = resolved;
        }

        StraumrWorkspaceEntry? workspaceEntry = optionsService.Options.CurrentWorkspace;
        if (workspaceEntry is null)
        {
            if (settings.Json)
                await System.Console.Error.WriteLineAsync("{\"error\":{\"message\":\"No workspace loaded\"}}");
            else
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
            StraumrAuth jsonAuth;
            try
            {
                jsonAuth = await authService.PeekByIdAsync(foundId.Value);
            }
            catch (StraumrException ex)
            {
                await System.Console.Error.WriteLineAsync(
                    $"{{\"error\":{{\"message\":\"{ex.Message}\"}}}}");
                return 1;
            }

            string configJson = JsonSerializer.Serialize(jsonAuth.Config,
                Straumr.Core.Configuration.StraumrJsonContext.Default.StraumrAuthConfig);
            string normalizedConfigJson = configJson.Replace("\"authType\":", "\"AuthType\":", StringComparison.Ordinal);
            JsonElement configElement = JsonDocument.Parse(normalizedConfigJson).RootElement;

            var result = new AuthGetResult(
                Id: jsonAuth.Id.ToString(),
                Name: jsonAuth.Name,
                Type: AuthTypeName(jsonAuth.Config),
                AutoRenewAuth: jsonAuth.AutoRenewAuth,
                LastAccessed: jsonAuth.LastAccessed.LocalDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                Modified: jsonAuth.Modified.LocalDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                Config: configElement
            );
            System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.AuthGetResult));
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

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }
}
