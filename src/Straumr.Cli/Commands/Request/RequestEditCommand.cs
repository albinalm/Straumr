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

public class RequestEditCommand(IStraumrRequestService requestService, IStraumrAuthService authService)
    : AsyncCommand<RequestEditCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<Name or ID>")] public required string Identifier { get; set; }
        [CommandOption("-e|--editor")] public bool UseEditor { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellation)
    {
        if (settings.UseEditor)
        {
            return await ExecuteEditorAsync(settings.Identifier, cancellation);
        }
        
        StraumrRequest request;
        try
        {
            request = await requestService.GetAsync(settings.Identifier);
        }
        catch (StraumrException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return -1;
        }
        
        return await ExecutePromptMenuAsync(request, cancellation);
    }

    private async Task<int> ExecutePromptMenuAsync(StraumrRequest request, CancellationToken cancellation)
    {
        var console = new EscapeCancellableConsole(AnsiConsole.Console);

        string name = request.Name;
        string url = request.Uri;
        string method = request.Method.Method;
        var parameters = new Dictionary<string, string>(request.Params, StringComparer.Ordinal);
        var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);
        var bodies = new Dictionary<BodyType, string>(request.Bodies);
        BodyType bodyType = request.BodyType;
        AuthType authType = request.AuthType;
        BearerAuthConfig? bearerAuth = request.BearerAuth;
        BasicAuthConfig? basicAuth = request.BasicAuth;
        OAuth2Config? oauth2 = request.OAuth2;
        CustomAuthConfig? customAuth = request.CustomAuth;

        const string actionFinish = "Save";
        const string actionName = "Edit name";
        const string actionUrl = "Edit URL";
        const string actionMethod = "Edit method";
        const string actionParams = "Edit params";
        const string actionHeaders = "Edit headers";
        const string actionBody = "Edit body";
        const string actionAuth = "Edit auth";
        const string actionFetchToken = "Fetch token";

        while (true)
        {
            string nameDisplay = $"[blue]{Markup.Escape(name)}[/]";
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
                actionFinish, actionName, actionUrl, actionMethod, actionParams, actionHeaders, actionBody, actionAuth
            };
            if ((authType == AuthType.OAuth2 && oauth2 is not null)
                || (authType == AuthType.Custom && customAuth is not null))
            {
                menuChoices.Add(actionFetchToken);
            }

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Edit request")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionName => $"Name: {nameDisplay}",
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
                    request.Name = name;
                    request.Uri = url;
                    request.Method = new HttpMethod(method);
                    request.Params = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
                    request.Headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
                    request.BodyType = bodyType;
                    request.Bodies = new Dictionary<BodyType, string>(bodies);
                    request.AuthType = authType;
                    request.BearerAuth = bearerAuth;
                    request.BasicAuth = basicAuth;
                    request.OAuth2 = oauth2;
                    request.CustomAuth = customAuth;

                    try
                    {
                        await requestService.UpdateAsync(request);
                        AnsiConsole.MarkupLine($"[green]Updated request[/] [bold]{request.Name}[/] ({request.Id})");
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
                case actionName:
                {
                    string? updated = await PromptAsync(console,
                        new TextPrompt<string>("Name")
                            .Validate(value => string.IsNullOrWhiteSpace(value)
                                ? ValidationResult.Error("Name cannot be empty.")
                                : ValidationResult.Success()));

                    if (!string.IsNullOrWhiteSpace(updated))
                    {
                        name = updated;
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

    private async Task<int> ExecuteEditorAsync(string identifier, CancellationToken cancellation)
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
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
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
                AnsiConsole.MarkupLine($"[red]Invalid request JSON: {Markup.Escape(ex.Message)}[/]");
                return 1;
            }

            if (deserializedJson is null)
            {
                AnsiConsole.MarkupLine("[red]Invalid request JSON.[/]");
                return 1;
            }

            if (deserializedJson.Id != requestId)
            {
                AnsiConsole.MarkupLine("[red]Request ID cannot be changed.[/]");
                return 1;
            }

            try
            {
                requestService.ApplyEdit(requestId, tempPath);
                AnsiConsole.MarkupLine(
                    $"[green]Updated request[/] [bold]{deserializedJson.Name}[/] ({deserializedJson.Id})");
                return 0;
            }
            catch (StraumrException ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
                return ex.Reason == StraumrError.EntryNotFound ? 1 : -1;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
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
