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
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Workspace;

public class WorkspaceEditCommand(IStraumrOptionsService optionsService, IStraumrWorkspaceService workspaceService)
    : AsyncCommand<WorkspaceEditCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (settings.Name is not null)
        {
            return await ExecuteInlineAsync(settings);
        }

        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            throw new StraumrException("No default editor is configured.", StraumrError.MissingEntry);
        }

        string tempPath;
        try
        {
            tempPath = await workspaceService.PrepareEditAsync(settings.Identifier);
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

        try
        {
            int? exitCode = await LaunchEditorAsync(editor, tempPath, cancellation);
            if (exitCode is not null)
            {
                return exitCode.Value;
            }

            try
            {
                await workspaceService.ApplyEditAsync(settings.Identifier, tempPath);

                if (settings.Json)
                {
                    StraumrWorkspaceEntry? entry =
                        await ResolveWorkspaceEntryAsync(settings.Identifier, optionsService, workspaceService);
                    if (entry is not null)
                    {
                        StraumrWorkspace workspace = await workspaceService.PeekWorkspaceAsync(entry.Path);
                        WorkspaceCreateResult result = new WorkspaceCreateResult(workspace.Id.ToString(), workspace.Name, entry.Path);
                        System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceCreateResult));
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[green]Workspace [bold]{settings.Identifier}[/] updated[/]");
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
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private async Task<int> ExecuteInlineAsync(Settings settings)
    {
        StraumrWorkspaceEntry entry;
        StraumrWorkspace workspace;
        try
        {
            entry = await ResolveWorkspaceEntryAsync(settings.Identifier, optionsService, workspaceService)
                ?? throw new StraumrException($"Workspace not found: {settings.Identifier}", StraumrError.EntryNotFound);
            workspace = await workspaceService.PeekWorkspaceAsync(entry.Path);
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

        workspace.Name = settings.Name!;

        try
        {
            await workspaceService.UpdateAsync(workspace);
            if (settings.Json)
            {
                WorkspaceCreateResult result = new WorkspaceCreateResult(workspace.Id.ToString(), workspace.Name, entry.Path);
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.WorkspaceCreateResult));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Workspace [bold]{workspace.Name}[/] updated[/]");
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
        [Description("Name or ID of the workspace to edit")]
        public required string Identifier { get; set; }

        [CommandOption("-n|--name")]
        [Description("The new name for the workspace")]
        public string? Name { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the updated workspace as JSON on success; errors emitted as JSON to stderr")]
        public bool Json { get; set; }
    }
}
