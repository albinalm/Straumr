using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Shared.Console;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Helpers.HttpCommandHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Request;

public class RequestEditCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IInteractiveConsole interactiveConsole)
    : AsyncCommand<RequestEditCommand.Settings>
{
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionUrl = "Edit URL";
    private const string ActionMethod = "Edit method";
    private const string ActionParams = "Edit params";
    private const string ActionHeaders = "Edit headers";
    private const string ActionBody = "Edit body";
    private const string ActionAuth = "Edit auth";

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (settings.Workspace is not null)
        {
            StraumrWorkspaceEntry? resolved =
                await ResolveWorkspaceEntryAsync(settings.Workspace, optionsService, workspaceService);
            if (resolved is null)
            {
                WriteError($"Workspace not found: {settings.Workspace}", settings.Json);
                return 1;
            }

            optionsService.Options.CurrentWorkspace = resolved;
        }

        bool hasWorkspace = optionsService.Options.CurrentWorkspace != null;

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

        if (settings.UseEditor || (settings.Json && !hasInlineFlags))
        {
            return await ExecuteEditorAsync(settings.Identifier, settings.Json, cancellation);
        }

        if (hasInlineFlags)
        {
            return await ExecuteInlineAsync(settings);
        }

        StraumrRequest request;
        try
        {
            request = await requestService.GetAsync(settings.Identifier);
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

        return await ExecutePromptMenuAsync(request, cancellation);
    }

    private async Task<int> ExecuteInlineAsync(Settings settings)
    {
        StraumrRequest request;
        try
        {
            request = await requestService.GetAsync(settings.Identifier);
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
                    StraumrAuth auth = await authService.GetAsync(settings.Auth);
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
            await requestService.UpdateAsync(request);
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

    private async Task<int> ExecutePromptMenuAsync(StraumrRequest request, CancellationToken cancellation)
    {
        IReadOnlyList<StraumrAuth> auths = await authService.ListAsync();
        EditableRequestState state = EditableRequestState.FromRequest(request);

        while (true)
        {
            string? action = await PromptEditMenuAsync(state, auths);
            if (action is null)
            {
                continue;
            }

            if (action == ActionSave)
            {
                if (await TrySaveChangesAsync(request, state))
                {
                    return 0;
                }

                continue;
            }

            await HandleEditActionAsync(state, action, cancellation);
        }
    }

    private async Task<string?> PromptEditMenuAsync(
        EditableRequestState state, IReadOnlyList<StraumrAuth> auths)
    {
        var nameDisplay = $"[blue]{Markup.Escape(state.Name)}[/]";
        string urlDisplay = string.IsNullOrWhiteSpace(state.Uri)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Uri)}[/]";
        var methodDisplay = $"[blue]{state.Method}[/]";
        string paramsDisplay = state.Params.Count == 0 ? "[grey]none[/]" : $"[blue]{state.Params.Count}[/]";
        string headersDisplay = state.Headers.Count == 0 ? "[grey]none[/]" : $"[blue]{state.Headers.Count}[/]";
        string bodyDisplay = state.BodyType == BodyType.None
            ? "[grey]none[/]"
            : $"[blue]{BodyTypeDisplayName(state.BodyType)}[/]";
        string authDisplay = AuthDisplayName(state.AuthId, auths);

        var menuChoices = new List<string>
        {
            ActionSave, ActionName, ActionUrl, ActionMethod, ActionParams, ActionHeaders, ActionBody, ActionAuth
        };

        return await interactiveConsole.SelectAsync("Edit request", menuChoices,
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

    private async Task<bool> TrySaveChangesAsync(StraumrRequest request, EditableRequestState state)
    {
        state.ApplyTo(request);

        try
        {
            await requestService.UpdateAsync(request);
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
        EditableRequestState state, string action, CancellationToken cancellation)
    {
        switch (action)
        {
            case ActionName:
            {
                string? updated = await interactiveConsole.TextInputAsync("Name", state.Name,
                    validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);

                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Name = updated;
                }

                break;
            }
            case ActionUrl:
            {
                string? updated = await PromptUrlAsync(interactiveConsole, state.Uri);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Uri = updated;
                }

                break;
            }
            case ActionMethod:
            {
                string? selected = await PromptMethodAsync(interactiveConsole);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    state.Method = selected;
                }

                break;
            }
            case ActionParams:
                await EditKeyValuePairsAsync(interactiveConsole, "Params", state.Params);
                break;
            case ActionHeaders:
                await EditKeyValuePairsAsync(interactiveConsole, "Headers", state.Headers);
                break;
            case ActionBody:
                state.BodyType =
                    await EditBodyAsync(interactiveConsole, state.Headers, state.Bodies, state.BodyType, cancellation);
                break;
            case ActionAuth:
            {
                StraumrAuth? selected = await SelectAuthAsync(interactiveConsole, authService);
                state.AuthId = selected?.Id;
                break;
            }
        }
    }

    private async Task<int> ExecuteEditorAsync(string identifier, bool json, CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        Guid requestId;
        string tempPath;
        try
        {
            (requestId, tempPath) = await requestService.PrepareEditAsync(identifier);
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

            if (deserializedJson.Id != requestId)
            {
                WriteError("Request ID cannot be changed.", json);
                return 1;
            }

            try
            {
                requestService.ApplyEdit(requestId, tempPath);
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

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")]
        [Description("Name or ID of the request to edit")]
        public required string Identifier { get; set; }

        [CommandOption("-e|--editor")]
        [Description("Open the request in the default editor instead of interactive prompts")]
        public bool UseEditor { get; set; }

        [CommandOption("-n|--name")]
        [Description("The new name for the request")]
        public string? Name { get; set; }

        [CommandOption("-u|--url")]
        [Description("New URL for the request")]
        public string? Url { get; set; }

        [CommandOption("-m|--method")]
        [Description("New HTTP method")]
        public string? Method { get; set; }

        [CommandOption("-H|--header")]
        [Description("Add or override a header in \"Name: Value\" format (repeatable)")]
        public string[]? Headers { get; set; }

        [CommandOption("-P|--param")]
        [Description("Add or override a query param in \"key=value\" format (repeatable)")]
        public string[]? Params { get; set; }

        [CommandOption("-d|--data")]
        [Description("New request body content")]
        public string? Data { get; set; }

        [CommandOption("-t|--type")]
        [Description("Body type: json, xml, text, form, multipart, raw, none")]
        public string? BodyType { get; set; }

        [CommandOption("-a|--auth")]
        [Description("Auth name or ID to link (use \"none\" to remove auth)")]
        public string? Auth { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the updated request as JSON; implies --editor when no inline flags are set")]
        public bool Json { get; set; }
    }

    private sealed class EditableRequestState
    {
        private EditableRequestState(string name, string uri, string method, Dictionary<string, string> parameters,
            Dictionary<string, string> headers, Dictionary<BodyType, string> bodies, BodyType bodyType,
            Guid? authId)
        {
            Name = name;
            Uri = uri;
            Method = method;
            Params = parameters;
            Headers = headers;
            Bodies = bodies;
            BodyType = bodyType;
            AuthId = authId;
        }

        public string Name { get; set; }
        public string Uri { get; set; }
        public string Method { get; set; }
        public Dictionary<string, string> Params { get; }
        public Dictionary<string, string> Headers { get; }
        public Dictionary<BodyType, string> Bodies { get; }
        public BodyType BodyType { get; set; }
        public Guid? AuthId { get; set; }

        public static EditableRequestState FromRequest(StraumrRequest request)
        {
            return new EditableRequestState(
                request.Name,
                request.Uri,
                request.Method.Method,
                new Dictionary<string, string>(request.Params, StringComparer.Ordinal),
                new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase),
                new Dictionary<BodyType, string>(request.Bodies),
                request.BodyType,
                request.AuthId);
        }

        public void ApplyTo(StraumrRequest request)
        {
            request.Name = Name;
            request.Uri = Uri;
            request.Method = new HttpMethod(Method);
            request.Params = new Dictionary<string, string>(Params, StringComparer.Ordinal);
            request.Headers = new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase);
            request.BodyType = BodyType;
            request.Bodies = new Dictionary<BodyType, string>(Bodies);
            request.AuthId = AuthId;
        }
    }
}
