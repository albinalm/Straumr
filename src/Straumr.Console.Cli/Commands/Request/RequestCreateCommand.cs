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
public class RequestCreateCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IStraumrFileService fileService,
    IInteractiveConsole interactiveConsole)
    : AsyncCommand<RequestCreateCommandSettings>
{
    private const string ActionFinish = "Finish";
    private const string ActionName = "Edit name";
    private const string ActionUrl = "Edit URL";
    private const string ActionMethod = "Edit method";
    private const string ActionParams = "Edit params";
    private const string ActionHeaders = "Edit headers";
    private const string ActionBody = "Edit body";
    private const string ActionAuth = "Edit auth";

    public override async Task<int> ExecuteAsync(CommandContext context, RequestCreateCommandSettings settings,
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
            WriteError("No workspace loaded. Please load a workspace using 'workspace use <name>'", settings.Json);
            return 1;
        }

        if (settings.Url is not null)
        {
            if (string.IsNullOrWhiteSpace(settings.Name))
            {
                WriteError("A name is required when creating a request inline.", settings.Json);
                return 1;
            }

            return await ExecuteInlineAsync(settings, workspaceEntry!);
        }

        if (!settings.UseEditor)
        {
            return await ExecutePromptMenuAsync(settings, workspaceEntry!, cancellation);
        }

        return await ExecuteFileEditAsync(settings, workspaceEntry!, cancellation);
    }

    private async Task<int> ExecutePromptMenuAsync(
        RequestCreateCommandSettings settings,
        StraumrWorkspaceEntry workspaceEntry,
        CancellationToken cancellation)
    {
        IReadOnlyList<StraumrAuth> auths = await authService.ListAsync(workspaceEntry);
        var state = new RequestCreateStateModel(settings.Name ?? string.Empty);

        while (true)
        {
            string? action = PromptCreateMenu(state, auths);
            if (action is null)
            {
                return 1;
            }

            if (action == ActionFinish)
            {
                if (await TryCreateRequestAsync(state, workspaceEntry))
                {
                    return 0;
                }

                continue;
            }

            await HandleCreateActionAsync(state, action, workspaceEntry, cancellation);
        }
    }

    private async Task<int> ExecuteFileEditAsync(
        RequestCreateCommandSettings settings,
        StraumrWorkspaceEntry workspaceEntry,
        CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".jsonc");
        try
        {
            var request = new StraumrRequest
            {
                Uri = string.Empty,
                Method = HttpMethod.Get,
                Name = settings.Name ?? string.Empty
            };

            string json = JsonSerializer.Serialize(request, StraumrJsonContext.Default.StraumrRequest);
            await File.WriteAllTextAsync(tempPath, json, cancellation);

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
                WriteError($"Invalid request JSON: {ex.Message}", false);
                return 1;
            }

            if (deserializedJson is null)
            {
                WriteError("Invalid request JSON.", false);
                return 1;
            }

            try
            {
                fileService.CarryCommentsFrom(
                    requestService.PathFor(workspaceEntry, deserializedJson.Id), editedJson);
                await requestService.CreateAsync(workspaceEntry, deserializedJson, cancellation);
                AnsiConsole.MarkupLine(
                    $"[green]Created request[/] [bold]{deserializedJson.Name}[/] ({deserializedJson.Id})");
                return 0;
            }
            catch (StraumrException ex)
            {
                WriteError(ex.Message, false);
            }
            catch (Exception ex)
            {
                WriteError(ex.Message, false);
            }

            return -1;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private string? PromptCreateMenu(
        RequestCreateStateModel state, IReadOnlyList<StraumrAuth> auths)
    {
        string nameDisplay = string.IsNullOrWhiteSpace(state.Name)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Name)}[/]";
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
            ActionFinish, ActionName, ActionUrl, ActionMethod, ActionParams, ActionHeaders, ActionBody, ActionAuth
        };

        return interactiveConsole.Select("Request setup", menuChoices,
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

    private async Task<bool> TryCreateRequestAsync(RequestCreateStateModel state, StraumrWorkspaceEntry workspaceEntry)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            interactiveConsole.ShowMessage("[red]A name is required.[/]");
            return false;
        }

        StraumrRequest request = state.ToRequest();
        try
        {
            await requestService.CreateAsync(workspaceEntry, request);
            AnsiConsole.MarkupLine($"[green]Created request[/] [bold]{request.Name}[/] ({request.Id})");
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

    private async Task HandleCreateActionAsync(
        RequestCreateStateModel state,
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
                EditKeyValuePairs(interactiveConsole, "Params", state.Params);
                break;
            case ActionHeaders:
                EditKeyValuePairs(interactiveConsole, "Headers", state.Headers);
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

    private async Task<int> ExecuteInlineAsync(RequestCreateCommandSettings settings, StraumrWorkspaceEntry workspaceEntry)
    {
        var request = new StraumrRequest
        {
            Name = settings.Name!,
            Uri = settings.Url!,
            Method = new HttpMethod(settings.Method ?? "GET")
        };

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
                null => BodyType.Json,
                _ => BodyType.None
            };

            if (bodyType == BodyType.None)
            {
                WriteError($"Unknown body type: {settings.BodyType!}. Use json, xml, text, form, multipart, or raw.", settings.Json);
                return 1;
            }

            request.BodyType = bodyType;
            request.Bodies[bodyType] = settings.Data;
        }

        if (settings.Auth is not null)
        {
            try
            {
                StraumrAuth auth = await GetAuthAsync(authService, workspaceEntry, settings.Auth);
                request.AuthId = auth.Id;
            }
            catch (StraumrException ex)
            {
                WriteError(ex.Message, settings.Json);
                return 1;
            }
        }

        try
        {
            await requestService.CreateAsync(workspaceEntry, request);

            if (settings.Json)
            {
                var result = new RequestCreateResult(
                    request.Id.ToString(),
                    request.Name,
                    request.Method.Method,
                    request.Uri);
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.RequestCreateResult));
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $"[green]Created request[/] [bold]{Markup.Escape(request.Name)}[/] ({request.Id})");
            }

            return 0;
        }
        catch (StraumrException ex)
        {
            WriteError(ex.Message, settings.Json);
            return 1;
        }
    }
}
