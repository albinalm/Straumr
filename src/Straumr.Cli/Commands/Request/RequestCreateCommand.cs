using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Cli.Console;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Console.PromptHelpers;
using static Straumr.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Cli.Commands.Request;

public class RequestCreateCommand(IStraumrRequestService requestService, IStraumrAuthService authService)
    : AsyncCommand<RequestCreateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name>")] public required string Name { get; set; }
        [CommandOption("-e|--editor")] public bool UseEditor { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (!settings.UseEditor)
        {
            return await ExecutePromptMenuAsync(settings, cancellation);
        }
        else
        {
            return await ExecuteFileEditAsync(settings, cancellation);
        }
    }

    private async Task<int> ExecutePromptMenuAsync(Settings settings,
        CancellationToken cancellation)
    {
        var console = new EscapeCancellableConsole(AnsiConsole.Console);
        var url = string.Empty;
        var method = "GET";
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bodyType = BodyType.None;
        var bodies = new Dictionary<BodyType, string>();
        var authType = AuthType.None;
        BearerAuthConfig? bearerAuth = null;
        BasicAuthConfig? basicAuth = null;
        OAuth2Config? oauth2 = null;
        CustomAuthConfig? customAuth = null;

        const string actionFinish = "Finish";
        const string actionUrl = "Edit URL";
        const string actionMethod = "Edit method";
        const string actionParams = "Edit params";
        const string actionHeaders = "Edit headers";
        const string actionBody = "Edit body";
        const string actionAuth = "Edit auth";
        const string actionFetchToken = "Fetch token";

        while (true)
        {
            string urlDisplay = string.IsNullOrWhiteSpace(url) ? "[grey]not set[/]" : $"[blue]{Markup.Escape(url)}[/]";
            var methodDisplay = $"[blue]{method}[/]";
            string paramsDisplay = parameters.Count == 0 ? "[grey]none[/]" : $"[blue]{parameters.Count}[/]";
            string headersDisplay = headers.Count == 0 ? "[grey]none[/]" : $"[blue]{headers.Count}[/]";
            string bodyDisplay = bodyType == BodyType.None
                ? "[grey]none[/]"
                : $"[blue]{BodyTypeDisplayName(bodyType)}[/]";
            string authDisplay = AuthDisplayName(authType, oauth2: oauth2, customAuth: customAuth,
                bearerAuth: bearerAuth, basicAuth: basicAuth);

            var menuChoices = new List<string>
            {
                actionFinish, actionUrl, actionMethod, actionParams, actionHeaders, actionBody, actionAuth
            };
            if ((authType == AuthType.OAuth2 && oauth2 is not null)
                || (authType == AuthType.Custom && customAuth is not null))
            {
                menuChoices.Add(actionFetchToken);
            }

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Request setup")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionUrl => $"URL: {urlDisplay}",
                    actionMethod => $"Method: {methodDisplay}",
                    actionParams => $"Params: {paramsDisplay}",
                    actionHeaders => $"Headers: {headersDisplay}",
                    actionBody => $"Body: {bodyDisplay}",
                    actionAuth => $"Auth: {authDisplay}",
                    _ => choice
                })
                .AddChoices(menuChoices);

            string? action = await PromptAsync(console, prompt);

            if (action is null)
            {
                continue;
            }

            switch (action)
            {
                case actionFinish:
                {
                    var request = new StraumrRequest
                    {
                        Name = settings.Name,
                        Uri = url,
                        Method = new HttpMethod(method),
                        Params = parameters,
                        Headers = headers,
                        BodyType = bodyType,
                        Bodies = bodies,
                        AuthType = authType,
                        BearerAuth = bearerAuth,
                        BasicAuth = basicAuth,
                        OAuth2 = oauth2,
                        CustomAuth = customAuth
                    };
                    try
                    {
                        await requestService.CreateAsync(request);
                        AnsiConsole.MarkupLine($"[green]Created request[/] [bold]{request.Name}[/] ({request.Id})");
                        return 0;
                    }
                    catch (StraumrException ex)
                    {
                        ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
                    }
                    catch (Exception ex)
                    {
                        ShowTransientMessage($"[red]{Markup.Escape(ex.Message)}[/]");
                    }

                    break;
                }
                case actionUrl:
                {
                    string? updated = await PromptUrlAsync(console);
                    if (!string.IsNullOrWhiteSpace(updated))
                    {
                        url = updated;
                    }

                    break;
                }
                case actionMethod:
                {
                    string? selected = await PromptMethodAsync(console);
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        method = selected;
                    }

                    break;
                }
                case actionParams:
                    await EditKeyValuePairsAsync(console, "Params", parameters);
                    break;
                case actionHeaders:
                    await EditKeyValuePairsAsync(console, "Headers", headers);
                    break;
                case actionBody:
                    bodyType = await EditBodyAsync(console, headers, bodies, bodyType, cancellation);
                    break;
                case actionAuth:
                    (authType, bearerAuth, basicAuth, oauth2, customAuth) =
                        await EditAuthAsync(console, authType, bearerAuth, basicAuth, oauth2, customAuth);
                    break;
                case actionFetchToken:
                {
                    try
                    {
                        if (authType == AuthType.OAuth2 && oauth2 is not null)
                        {
                            OAuth2Token token = await authService.FetchTokenAsync(oauth2);
                            oauth2.Token = token;
                            string expiresDisplay = token.ExpiresAt.HasValue
                                ? token.ExpiresAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC")
                                : "N/A";
                            ShowTransientMessage(
                                $"[green]Token fetched successfully![/]\n" +
                                $"Type: [blue]{Markup.Escape(token.TokenType)}[/]\n" +
                                $"Expires: [blue]{expiresDisplay}[/]");
                        }
                        else if (authType == AuthType.Custom && customAuth is not null)
                        {
                            string value = await authService.ExecuteCustomAuthAsync(customAuth);
                            string headerPreview = customAuth.ApplyHeaderTemplate.Replace("{{value}}", value);
                            ShowTransientMessage(
                                $"[green]Value fetched successfully![/]\n" +
                                $"Extracted: [blue]{Markup.Escape(value)}[/]\n" +
                                $"Header: [blue]{Markup.Escape(customAuth.ApplyHeaderName)}: {Markup.Escape(headerPreview)}[/]");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowTransientMessage($"[red]Failed to fetch: {Markup.Escape(ex.Message)}[/]");
                    }

                    break;
                }
            }
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

}
