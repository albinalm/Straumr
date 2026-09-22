using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Workspace;

[UsedImplicitly]
public class WorkspaceEditCommand(
    IStraumrWorkspaceService workspaceService,
    IStraumrFileService fileService)
    : AsyncCommand<WorkspaceEditCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, WorkspaceEditCommandSettings settings,
        CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(editor))
        {
            throw new StraumrException("No default editor is configured.", StraumrError.MissingEntry);
        }

        StraumrWorkspace original;
        string tempPath;
        try
        {
            original = await GetWorkspaceAsync(
                workspaceService, settings.Identifier, cancellationToken: cancellation);
            tempPath = await CreateEditorFileAsync(
                original, StraumrJsonContext.Default.StraumrWorkspace, cancellation,
                workspaceService.GetEntry(original.Id).Path);
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
                string editedJson = await File.ReadAllTextAsync(tempPath, cancellation);
                StraumrWorkspace? workspace = JsonSerializer.Deserialize(
                    editedJson, StraumrJsonContext.Default.StraumrWorkspace);
                if (workspace is null)
                {
                    WriteError("Invalid workspace JSON.", settings.Json);
                    return 1;
                }

                if (workspace.Id != original.Id)
                {
                    WriteError("Workspace ID cannot be changed.", settings.Json);
                    return 1;
                }

                fileService.CarryCommentsFrom(
                    workspaceService.GetEntry(workspace.Id).Path, editedJson);
                await workspaceService.SaveAsync(workspace, cancellation);

                if (settings.Json)
                {
                    StraumrWorkspaceEntry entry = workspaceService.GetEntry(workspace.Id);
                    var result = new WorkspaceCreateResult(
                        workspace.Id.ToString(), workspace.Name, entry.Path);
                    System.Console.WriteLine(JsonSerializer.Serialize(
                        result, CliJsonContext.Relaxed.WorkspaceCreateResult));
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
}
