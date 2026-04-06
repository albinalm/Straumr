using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Console;
using Straumr.Cli.Infrastructure;
using Straumr.Cli.Models;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Cli.Helpers.ConsoleHelpers;
using static Straumr.Cli.Helpers.HttpCommandHelpers;
using static Straumr.Cli.Console.PromptHelpers;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Cli.Commands.Request;

public class RequestCreateCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService)
    : AsyncCommand<RequestCreateCommand.Settings>
{
    private const string ActionFinish = "Finish";
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

            return await ExecuteInlineAsync(settings);
        }

        if (!settings.UseEditor)
        {
            return await ExecutePromptMenuAsync(settings, cancellation);
        }

        return await ExecuteFileEditAsync(settings, cancellation);
    }

    private async Task<int> ExecutePromptMenuAsync(Settings settings, CancellationToken cancellation)
    {
        var console = new EscapeCancellableConsole(AnsiConsole.Console);
        IReadOnlyList<StraumrAuth> auths = await authService.ListAsync();
        var state = new CreateRequestState(settings.Name ?? string.Empty);

        while (true)
        {
            string? action = await PromptCreateMenuAsync(console, state, auths);
            if (action is null)
            {
                continue;
            }

            if (action == ActionFinish)
            {
                if (await TryCreateRequestAsync(state))
                {
                    return 0;
                }

                continue;
            }

            await HandleCreateActionAsync(console, state, action, cancellation);
        }
    }

    private async Task<int> ExecuteFileEditAsync(Settings settings, CancellationToken cancellation)
    {
        string? editor = Environment.GetEnvironmentVariable("EDITOR");
        if (editor is null)
        {
            throw new StraumrException("No default editor configured", StraumrError.MissingEntry);
        }

        string tempPath = Path.GetTempFileName();
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
                await requestService.CreateAsync(deserializedJson);
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

    private static async Task<string?> PromptCreateMenuAsync(
        EscapeCancellableConsole console, CreateRequestState state, IReadOnlyList<StraumrAuth> auths)
    {
        string nameDisplay = string.IsNullOrWhiteSpace(state.Name)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Name)}[/]";
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
            ActionFinish, ActionName, ActionUrl, ActionMethod, ActionParams, ActionHeaders, ActionBody, ActionAuth
        };

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Request setup")
            .EnableSearch()
            .SearchPlaceholderText("/")
            .UseConverter(choice => choice switch
            {
                ActionName => $"Name: {nameDisplay}",
                ActionUrl => $"URL: {urlDisplay}",
                ActionMethod => $"Method: {methodDisplay}",
                ActionParams => $"Params: {paramsDisplay}",
                ActionHeaders => $"Headers: {headersDisplay}",
                ActionBody => $"Body: {bodyDisplay}",
                ActionAuth => $"Auth: {authDisplay}",
                _ => choice
            })
            .AddChoices(menuChoices);

        return await PromptAsync(console, prompt);
    }

    private async Task<bool> TryCreateRequestAsync(CreateRequestState state)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            ShowTransientMessage("[red]A name is required.[/]");
            return false;
        }

        StraumrRequest request = state.ToRequest();
        try
        {
            await requestService.CreateAsync(request);
            AnsiConsole.MarkupLine($"[green]Created request[/] [bold]{request.Name}[/] ({request.Id})");
            return true;
        }
        catch (StraumrException ex)
        {
            ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
        }
        catch (Exception ex)
        {
            ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
        }

        return false;
    }

    private async Task HandleCreateActionAsync(
        EscapeCancellableConsole console, CreateRequestState state, string action, CancellationToken cancellation)
    {
        switch (action)
        {
            case ActionName:
            {
                TextPrompt<string> prompt = new TextPrompt<string>("Name")
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("Name cannot be empty.")
                        : ValidationResult.Success());
                string? updated = await PromptTextAsync(console, prompt, state.Name);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Name = updated;
                }

                break;
            }
            case ActionUrl:
            {
                string? updated = await PromptUrlAsync(console, state.Uri);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Uri = updated;
                }

                break;
            }
            case ActionMethod:
            {
                string? selected = await PromptMethodAsync(console);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    state.Method = selected;
                }

                break;
            }
            case ActionParams:
                await EditKeyValuePairsAsync(console, "Params", state.Params);
                break;
            case ActionHeaders:
                await EditKeyValuePairsAsync(console, "Headers", state.Headers);
                break;
            case ActionBody:
                state.BodyType =
                    await EditBodyAsync(console, state.Headers, state.Bodies, state.BodyType, cancellation);
                break;
            case ActionAuth:
            {
                StraumrAuth? selected = await SelectAuthAsync(console, authService);
                state.AuthId = selected?.Id;
                break;
            }
        }
    }

    private async Task<int> ExecuteInlineAsync(Settings settings)
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
                StraumrAuth auth = await authService.GetAsync(settings.Auth);
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
            await requestService.CreateAsync(request);

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

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[Name]")]
        [Description("Name of the request to create")]
        public string? Name { get; set; }

        [CommandArgument(1, "[Url]")]
        [Description("Request URL. When provided, creates the request directly without interactive prompts.")]
        public string? Url { get; set; }

        [CommandOption("-m|--method")]
        [Description("HTTP method (default: GET)")]
        public string? Method { get; set; }

        [CommandOption("-H|--header")]
        [Description("Header in \"Name: Value\" format (repeatable)")]
        public string[]? Headers { get; set; }

        [CommandOption("-P|--param")]
        [Description("Query param in \"key=value\" format (repeatable)")]
        public string[]? Params { get; set; }

        [CommandOption("-d|--data")]
        [Description("Request body content")]
        public string? Data { get; set; }

        [CommandOption("-t|--type")]
        [Description("Body type: json, xml, text, form, multipart, raw (default: json)")]
        public string? BodyType { get; set; }

        [CommandOption("-a|--auth")]
        [Description("Auth name or ID to link")]
        public string? Auth { get; set; }

        [CommandOption("-e|--editor")]
        [Description("Open the request in the default editor instead of interactive prompts")]
        public bool UseEditor { get; set; }

        [CommandOption("-j|--json")]
        [Description("Output the created request as JSON")]
        public bool Json { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }

    private sealed class CreateRequestState(string name)
    {
        public string Name { get; set; } = name;
        public string Uri { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public Dictionary<string, string> Params { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public BodyType BodyType { get; set; } = BodyType.None;
        public Dictionary<BodyType, string> Bodies { get; } = new();
        public Guid? AuthId { get; set; }

        public StraumrRequest ToRequest()
        {
            return new StraumrRequest
            {
                Name = Name,
                Uri = Uri,
                Method = new HttpMethod(Method),
                Params = Params,
                Headers = Headers,
                BodyType = BodyType,
                Bodies = Bodies,
                AuthId = AuthId
            };
        }
    }
}
