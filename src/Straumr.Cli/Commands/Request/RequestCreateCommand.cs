using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Console;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Cli.Helpers.HttpCommandHelpers;
using static Straumr.Cli.Console.PromptHelpers;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Cli.Commands.Request;

public class RequestCreateCommand(
    IStraumrOptionsService optionsService,
    IStraumrRequestService requestService,
    IStraumrAuthService authService,
    IStraumrAuthTemplateService authTemplateService)
    : AsyncCommand<RequestCreateCommand.Settings>
{
    private const string ActionFinish = "Finish";
    private const string ActionUrl = "Edit URL";
    private const string ActionMethod = "Edit method";
    private const string ActionParams = "Edit params";
    private const string ActionHeaders = "Edit headers";
    private const string ActionBody = "Edit body";
    private const string ActionAuth = "Edit auth";
    private const string ActionAutoRenew = "Auto-renew auth";
    private const string ActionFetchToken = "Fetch token";

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        bool hasWorkspace = optionsService.Options.CurrentWorkspace != null;

        if (!hasWorkspace)
        {
            throw new StraumrException("No workspace loaded. Please load a workspace using 'workspace use <name>'",
                StraumrError.MissingEntry);
        }

        if (settings.Url is not null)
        {
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
        var state = new CreateRequestState(settings.Name);

        while (true)
        {
            string? action = await PromptCreateMenuAsync(console, state);
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
                Name = settings.Name
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
                AnsiConsole.MarkupLine($"[red]Invalid request JSON: {Markup.Escape(ex.Message)}[/]");
                return 1;
            }

            if (deserializedJson is null)
            {
                AnsiConsole.MarkupLine("[red]Invalid request JSON.[/]");
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
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
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
        EscapeCancellableConsole console, CreateRequestState state)
    {
        string urlDisplay = string.IsNullOrWhiteSpace(state.Uri)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Uri)}[/]";
        var methodDisplay = $"[blue]{state.Method}[/]";
        string paramsDisplay = state.Params.Count == 0 ? "[grey]none[/]" : $"[blue]{state.Params.Count}[/]";
        string headersDisplay = state.Headers.Count == 0 ? "[grey]none[/]" : $"[blue]{state.Headers.Count}[/]";
        string bodyDisplay = state.BodyType == BodyType.None
            ? "[grey]none[/]"
            : $"[blue]{BodyTypeDisplayName(state.BodyType)}[/]";
        string authDisplay = AuthDisplayName(state.Auth);
        string autoRenewDisplay = state.AutoRenewAuth ? "[green]enabled[/]" : "[grey]disabled[/]";

        var menuChoices = new List<string>
        {
            ActionFinish, ActionUrl, ActionMethod, ActionParams, ActionHeaders, ActionBody, ActionAuth
        };

        if (SupportsAuthFetch(state.Auth))
        {
            menuChoices.Add(ActionFetchToken);
        }

        if (SupportsAuthAutoRenew(state.Auth))
        {
            menuChoices.Add(ActionAutoRenew);
        }

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Request setup")
            .EnableSearch()
            .SearchPlaceholderText("/")
            .UseConverter(choice => choice switch
            {
                ActionUrl => $"URL: {urlDisplay}",
                ActionMethod => $"Method: {methodDisplay}",
                ActionParams => $"Params: {paramsDisplay}",
                ActionHeaders => $"Headers: {headersDisplay}",
                ActionBody => $"Body: {bodyDisplay}",
                ActionAuth => $"Auth: {authDisplay}",
                ActionAutoRenew => $"Auto-renew auth: {autoRenewDisplay}",
                _ => choice
            })
            .AddChoices(menuChoices);

        return await PromptAsync(console, prompt);
    }

    private async Task<bool> TryCreateRequestAsync(CreateRequestState state)
    {
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
                state.Auth = await EditAuthAsync(console, state.Auth, authTemplateService);
                break;
            case ActionFetchToken:
                await FetchAuthValueAsync(authService, state.Auth);
                break;
            case ActionAutoRenew:
                state.AutoRenewAuth = !state.AutoRenewAuth;
                break;
        }
    }

    private async Task<int> ExecuteInlineAsync(Settings settings)
    {
        var request = new StraumrRequest
        {
            Name = settings.Name,
            Uri = settings.Url!,
            Method = new HttpMethod(settings.Method ?? "GET"),
            AutoRenewAuth = !settings.NoAutoRenew
        };

        foreach (string header in settings.Headers ?? [])
        {
            int colon = header.IndexOf(':');
            if (colon < 0)
            {
                AnsiConsole.MarkupLine($"[red]Invalid header (expected \"Name: Value\"): {Markup.Escape(header)}[/]");
                return 1;
            }

            request.Headers[header[..colon].Trim()] = header[(colon + 1)..].Trim();
        }

        foreach (string param in settings.Params ?? [])
        {
            int eq = param.IndexOf('=');
            if (eq < 0)
            {
                AnsiConsole.MarkupLine($"[red]Invalid param (expected \"key=value\"): {Markup.Escape(param)}[/]");
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
                AnsiConsole.MarkupLine(
                    $"[red]Unknown body type: {Markup.Escape(settings.BodyType!)}. Use json, xml, text, form, multipart, or raw.[/]");
                return 1;
            }

            request.BodyType = bodyType;
            request.Bodies[bodyType] = settings.Data;
        }

        if (settings.AuthTemplate is not null)
        {
            try
            {
                StraumrAuthTemplate template = await authTemplateService.GetAsync(settings.AuthTemplate);
                request.Auth = template.Config;
            }
            catch (StraumrException ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
                return 1;
            }
        }

        try
        {
            await requestService.CreateAsync(request);
            AnsiConsole.MarkupLine($"[green]Created request[/] [bold]{Markup.Escape(request.Name)}[/] ({request.Id})");
            return 0;
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name>")] public required string Name { get; set; }

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
        [Description("Auth template name or ID to apply")]
        public string? AuthTemplate { get; set; }

        [CommandOption("--no-auto-renew")]
        [Description("Disable automatic auth token renewal")]
        public bool NoAutoRenew { get; set; }

        [CommandOption("-e|--editor")] public bool UseEditor { get; set; }
    }

    private sealed class CreateRequestState(string name)
    {
        private string Name { get; } = name;
        public string Uri { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public Dictionary<string, string> Params { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public BodyType BodyType { get; set; } = BodyType.None;
        public Dictionary<BodyType, string> Bodies { get; } = new();
        public StraumrAuthConfig? Auth { get; set; }
        public bool AutoRenewAuth { get; set; } = true;

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
                Auth = Auth,
                AutoRenewAuth = AutoRenewAuth
            };
        }
    }
}