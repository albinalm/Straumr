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

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name>")] public required string Name { get; set; }
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
