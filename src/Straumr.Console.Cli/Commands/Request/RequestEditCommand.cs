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
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Helpers.HttpCommandHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Request;

[UsedImplicitly]
public class RequestEditCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IStraumrFileService fileService,
    IInteractiveConsole interactiveConsole)
    : AsyncCommand<RequestEditCommandSettings>
{
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionUrl = "Edit URL";
    private const string ActionMethod = "Edit method";
    private const string ActionParams = "Edit params";
    private const string ActionHeaders = "Edit headers";
    private const string ActionBody = "Edit body";
    private const string ActionAuth = "Edit auth";

    public override async Task<int> ExecuteAsync(CommandContext context, RequestEditCommandSettings settings,
        CancellationToken cancellation)
    {
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

        bool hasWorkspace = workspaceEntry != null;

        if (!hasWorkspace)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        bool hasInlineFlags = settings.Name is not null || settings.Url is not null ||
                              settings.Method is not null ||
                              settings.Headers?.Length > 0 || settings.Params?.Length > 0 ||
                              settings.Data is not null || settings.BodyType is not null ||
                              settings.Auth is not null;

        if (settings.UseEditor || settings.Json && !hasInlineFlags)
        {
            return await ExecuteEditorAsync(settings.Identifier, settings.Json, workspaceEntry!, cancellation);
        }

        if (hasInlineFlags)
        {
            return await ExecuteInlineAsync(settings, workspaceEntry!, cancellation);
        }

        StraumrRequest request;
        try
        {
            request = await GetRequestAsync(
                requestService, workspaceEntry!, settings.Identifier, cancellationToken: cancellation);
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, false);
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, false);
            return -1;
        }

        return await ExecutePromptMenuAsync(request, workspaceEntry!, cancellation);
    }

    private async Task<int> ExecuteInlineAsync(
        RequestEditCommandSettings settings,
        StraumrWorkspaceEntry workspaceEntry,
        CancellationToken cancellation)
    {
        StraumrRequest request;
        try
        {
            request = await GetRequestAsync(
                requestService, workspaceEntry, settings.Identifier, cancellationToken: cancellation);
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

        if (settings.Name is not null)
        {
            request.Name = settings.Name;
        }

        if (settings.Url is not null)
        {
            request.Uri = settings.Url;
        }

        if (settings.Method is not null)
        {
            request.Method = new HttpMethod(settings.Method);
        }

        foreach (string header in settings.Headers ?? [])
        {
            int colon = header.IndexOf(':');
            if (colon < 0)
            {
                WriteError($"Invalid header (expected \"Name: Value\"): {header}", settings.Json);
                return 1;
            }

            request.Headers[header[..colon].Trim()] = header[(colon + 1)..].Trim();
        }

        foreach (string param in settings.Params ?? [])
        {
            int eq = param.IndexOf('=');
            if (eq < 0)
            {
                WriteError($"Invalid param (expected \"key=value\"): {param}", settings.Json);
                return 1;
            }

            request.Params[param[..eq]] = param[(eq + 1)..];
        }

        if (settings.Data is not null)
        {
            BodyType bodyType = settings.BodyType?.ToLowerInvariant() switch
            {
                "json" => BodyType.Json,
                "xml" => BodyType.Xml,
                "text" => BodyType.Text,
                "form" => BodyType.FormUrlEncoded,
                "multipart" => BodyType.MultipartForm,
                "raw" => BodyType.Raw,
                null => request.BodyType == BodyType.None ? BodyType.Json : request.BodyType,
                _ => BodyType.None
            };

            if (bodyType == BodyType.None)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Unknown body type: {Markup.Escape(settings.BodyType!)}. Use json, xml, text, form, multipart, or raw.[/]");
                return 1;
            }

            request.BodyType = bodyType;
            request.Bodies[bodyType] = settings.Data;
        }
        else if (settings.BodyType is not null)
        {
            BodyType bodyType = settings.BodyType.ToLowerInvariant() switch
            {
                "json" => BodyType.Json,
                "xml" => BodyType.Xml,
                "text" => BodyType.Text,
                "form" => BodyType.FormUrlEncoded,
                "multipart" => BodyType.MultipartForm,
                "raw" => BodyType.Raw,
                "none" => BodyType.None,
                _ => (BodyType)(-1)
            };

            if ((int)bodyType == -1)
            {
                WriteError($"Unknown body type: {settings.BodyType}. Use json, xml, text, form, multipart, raw, or none.", settings.Json);
                return 1;
            }

            request.BodyType = bodyType;
        }

        if (settings.Auth is not null)
        {
            if (string.Equals(settings.Auth, "none", StringComparison.OrdinalIgnoreCase))
            {
                request.AuthId = null;
            }
            else
            {
                try
                {
                    StraumrAuth auth = await GetAuthAsync(
                        authService, workspaceEntry, settings.Auth, cancellationToken: cancellation);
                    request.AuthId = auth.Id;
                }
                catch (StraumrException ex)
                {
                    WriteError(ex.Message, settings.Json);
                    return 1;
                }
            }
        }

        try
        {
            await requestService.SaveAsync(workspaceEntry, request, cancellation);
            if (settings.Json)
            {
                var result = new RequestCreateResult(request.Id.ToString(), request.Name, request.Method.Method, request.Uri);
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.RequestCreateResult));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Updated request[/] [bold]{request.Name}[/] ({request.Id})");
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

    private async Task<int> ExecutePromptMenuAsync(
        StraumrRequest request,
        StraumrWorkspaceEntry workspaceEntry,
        CancellationToken cancellation)
    {
        IReadOnlyList<StraumrAuth> auths = await authService.ListAsync(workspaceEntry);
        RequestEditStateModel state = RequestEditStateModel.FromRequest(request);

        while (true)
        {
            string? action = PromptEditMenu(state, auths);
            if (action is null)
            {
                return 1;
            }

            if (action == ActionSave)
            {
                if (await TrySaveChangesAsync(request, state, workspaceEntry))
                {
                    return 0;
                }

                continue;
            }

            await HandleEditActionAsync(request, state, action, workspaceEntry, cancellation);
        }
    }

    private string? PromptEditMenu(
        RequestEditStateModel state, IReadOnlyList<StraumrAuth> auths)
    {
        string nameDisplay = $"[blue]{Markup.Escape(state.Name)}[/]";
        string urlDisplay = string.IsNullOrWhiteSpace(state.Uri)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Uri)}[/]";
        string methodDisplay = $"[blue]{state.Method}[/]";
        string paramsDisplay = state.Params.Count == 0 ? "[grey]none[/]" : $"[blue]{state.Params.Count}[/]";
        string headersDisplay = state.Headers.Count == 0 ? "[grey]none[/]" : $"[blue]{state.Headers.Count}[/]";
        string bodyDisplay = state.BodyType == BodyType.None
            ? "[grey]none[/]"
            : $"[blue]{BodyTypeDisplayName(state.BodyType)}[/]";
        string authDisplay = AuthDisplayName(state.AuthId, auths);

        List<string> menuChoices = new()
        {
            ActionSave, ActionName, ActionUrl, ActionMethod, ActionParams, ActionHeaders, ActionBody, ActionAuth
        };

        return interactiveConsole.Select("Edit request", menuChoices,
            choice => choice switch
            {
                ActionName => $"Name: {nameDisplay}",
                ActionUrl => $"URL: {urlDisplay}",
                ActionMethod => $"Method: {methodDisplay}",
                ActionParams => $"Params: {paramsDisplay}",
                ActionHeaders => $"Headers: {headersDisplay}",
                ActionBody => $"Body: {bodyDisplay}",
                ActionAuth => $"Auth: {authDisplay}",
                _ => choice
            });
    }

    private void SaveStateQuietly(
        StraumrRequest request,
        RequestEditStateModel state,
        StraumrWorkspaceEntry workspaceEntry)
    {
        state.ApplyTo(request);
        try { requestService.SaveAsync(workspaceEntry, request).GetAwaiter().GetResult(); }
        catch { }
    }

    private async Task<bool> TrySaveChangesAsync(
        StraumrRequest request,
        RequestEditStateModel state,
        StraumrWorkspaceEntry workspaceEntry)
    {
        state.ApplyTo(request);

        try
        {
            await requestService.SaveAsync(workspaceEntry, request);
            AnsiConsole.MarkupLine($"[green]Updated request[/] [bold]{request.Name}[/] ({request.Id})");
            return true;
        }
        catch (StraumrException ex)
        {
            interactiveConsole.ShowMessage($"[red]{Markup.Escape(ex.Message)}[/]");
        }
        catch (Exception ex)
        {
            interactiveConsole.ShowMessage($"[red]{Markup.Escape(ex.Message)}[/]");
        }

        return false;
    }

    private async Task HandleEditActionAsync(
        StraumrRequest request,
        RequestEditStateModel state,
        string action,
        StraumrWorkspaceEntry workspaceEntry,
        CancellationToken cancellation)
    {
        switch (action)
        {
            case ActionName:
                {
                    string? updated = interactiveConsole.TextInput("Name", state.Name,
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);

                    if (!string.IsNullOrWhiteSpace(updated))
                    {
                        state.Name = updated;
                    }

                    break;
                }
            case ActionUrl:
                {
                    string? updated = PromptUrl(interactiveConsole, state.Uri);
                    if (!string.IsNullOrWhiteSpace(updated))
                    {
                        state.Uri = updated;
                    }

                    break;
                }
            case ActionMethod:
                {
                    string? selected = PromptMethod(interactiveConsole);
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        state.Method = selected;
                    }

                    break;
                }
            case ActionParams:
                EditKeyValuePairs(interactiveConsole, "Params", state.Params,
                    () => SaveStateQuietly(request, state, workspaceEntry));
                break;
            case ActionHeaders:
                EditKeyValuePairs(interactiveConsole, "Headers", state.Headers,
                    () => SaveStateQuietly(request, state, workspaceEntry));
                break;
            case ActionBody:
                state.BodyType =
                    await EditBodyAsync(interactiveConsole, state.Headers, state.Bodies, state.BodyType, cancellation);
                break;
            case ActionAuth:
                {
                    StraumrAuth? selected = await SelectAuthAsync(interactiveConsole, authService, workspaceEntry);
                    state.AuthId = selected?.Id;
                    break;
                }
        }
    }

    private async Task<int> ExecuteEditorAsync(
        string identifier,
        bool json,
        StraumrWorkspaceEntry workspaceEntry,
        CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        StraumrRequest request;
        string tempPath;
        try
        {
            request = await GetRequestAsync(
                requestService, workspaceEntry, identifier, cancellationToken: cancellation);
            tempPath = await CreateEditorFileAsync(
                request, StraumrJsonContext.Default.StraumrRequest, cancellation,
                requestService.PathFor(workspaceEntry, request.Id));
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, json);
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, json);
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
            StraumrRequest? deserializedJson;
            try
            {
                deserializedJson = JsonSerializer.Deserialize<StraumrRequest>(editedJson,
                    StraumrJsonContext.Default.StraumrRequest);
            }
            catch (JsonException ex)
            {
                WriteError($"Invalid request JSON: {ex.Message}", json);
                return 1;
            }

            if (deserializedJson is null)
            {
                WriteError("Invalid request JSON.", json);
                return 1;
            }

            if (deserializedJson.Id != request.Id)
            {
                WriteError("Request ID cannot be changed.", json);
                return 1;
            }

            try
            {
                fileService.CarryCommentsFrom(
                    requestService.PathFor(workspaceEntry, deserializedJson.Id), editedJson);
                await requestService.SaveAsync(workspaceEntry, deserializedJson, cancellation);
                if (json)
                {
                    var result = new RequestCreateResult(deserializedJson.Id.ToString(), deserializedJson.Name, deserializedJson.Method.Method, deserializedJson.Uri);
                    System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.RequestCreateResult));
                }
                else
                {
                    AnsiConsole.MarkupLine(
                        $"[green]Updated request[/] [bold]{deserializedJson.Name}[/] ({deserializedJson.Id})");
                }
                return 0;
            }
            catch (StraumrException ex)
            {
                WriteError(ex.Message, json);
                return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
            }
            catch (Exception ex)
            {
                WriteError(ex.Message, json);
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
