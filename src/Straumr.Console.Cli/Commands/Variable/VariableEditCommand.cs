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

namespace Straumr.Console.Cli.Commands.Variable;

[UsedImplicitly]
public class VariableEditCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrVariableService variableService,
    IStraumrFileService fileService) : AsyncCommand<VariableEditCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VariableEditCommandSettings settings,
        CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        StraumrWorkspaceEntry? workspaceEntry = stateService.State.CurrentWorkspace;

        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, workspaceService);
            if (resolved is null)
            {
                WriteError($"Workspace not found: {settings.Workspace}", settings.Json);
                return 1;
            }

            workspaceEntry = resolved;
        }

        if (workspaceEntry is null)
        {
            WriteError("No workspace loaded. Please load a workspace using 'workspace use <name>'", settings.Json);
            return 1;
        }

        StraumrVariable variable;
        string tempPath;
        try
        {
            variable = await GetVariableAsync(
                variableService, workspaceEntry, settings.Identifier, cancellationToken: cancellation);
            tempPath = await CreateEditorFileAsync(
                variable, StraumrJsonContext.Default.StraumrVariable, cancellation,
                variableService.PathFor(workspaceEntry, variable.Id));
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

            string editedJson = await File.ReadAllTextAsync(tempPath, cancellation);
            StraumrVariable? deserialized;
            try
            {
                deserialized = JsonSerializer.Deserialize(editedJson, StraumrJsonContext.Default.StraumrVariable);
            }
            catch (JsonException ex)
            {
                WriteError($"Invalid variable JSON: {ex.Message}", settings.Json);
                return 1;
            }

            if (deserialized is null)
            {
                WriteError("Invalid variable JSON.", settings.Json);
                return 1;
            }

            if (deserialized.Id != variable.Id)
            {
                WriteError("Variable ID cannot be changed.", settings.Json);
                return 1;
            }

            try
            {
                fileService.CarryCommentsFrom(
                    variableService.PathFor(workspaceEntry, deserialized.Id), editedJson);
                await variableService.SaveAsync(workspaceEntry, deserialized, cancellation);
                if (settings.Json)
                {
                    var result = new VariableListItem(deserialized.Id.ToString(), deserialized.Name, "Valid");
                    System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.VariableListItem));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[green]Updated variable[/] [bold]{Markup.Escape(deserialized.Name)}[/] ({deserialized.Id})");
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
