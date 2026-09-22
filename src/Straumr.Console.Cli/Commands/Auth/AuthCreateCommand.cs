using System.Text.Json;
using JetBrains.Annotations;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public class AuthCreateCommand(
    IStraumrStateService stateService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService,
    IInteractiveConsole interactiveConsole)
    : AsyncCommand<AuthCreateCommandSettings>
{
    private const string ActionFinish = "Finish";
    private const string ActionName = "Edit name";
    private const string ActionConfigure = "Configure auth";
    private const string ActionFetch = "Fetch token/value";
    private const string ActionAutoRenew = "Auto-renew auth";

    public override async Task<int> ExecuteAsync(CommandContext context, AuthCreateCommandSettings settings,
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

        if (settings.Type is not null && workspaceEntry is not null)
        {
            return await ExecuteInlineAsync(settings, workspaceEntry);
        }

        var state = new AuthCreateStateModel(settings.Name ?? string.Empty);

        while (true)
        {
            string? action = PromptCreateMenu(state);
            if (action is null)
            {
                return 1;
            }

            if (action == ActionFinish && workspaceEntry is not null)
            {
                if (await TryCreateAuthAsync(state, workspaceEntry))
                {
                    return 0;
                }

                continue;
            }

            await HandleCreateActionAsync(state, action, cancellation);
        }
    }

    private async Task<int> ExecuteInlineAsync(AuthCreateCommandSettings settings, StraumrWorkspaceEntry workspaceEntry)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            WriteError("A name is required when creating auth inline.", settings.Json);
            return 1;
        }

        StraumrAuthConfig? config = BuildInlineConfig(settings);
        if (config is null)
        {
            return 1;
        }

        var auth = new StraumrAuth
        {
            Name = settings.Name!,
            Config = config,
            AutoRenewAuth = settings.AutoRenew
        };

        try
        {
            await authService.CreateAsync(workspaceEntry, auth);

            if (settings.Json)
            {
                var result = new AuthListItem(auth.Id.ToString(), auth.Name, AuthTypeName(auth.Config));
                System.Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Relaxed.AuthListItem));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Created auth[/] [bold]{Markup.Escape(auth.Name)}[/] ({auth.Id})");
            }

            return 0;
        }
        catch (Exception ex)
        {
            WriteError(ex.Message, settings.Json);
            return 1;
        }
    }

    private StraumrAuthConfig? BuildInlineConfig(AuthCreateCommandSettings settings)
    {
        switch (settings.Type!.ToLowerInvariant())
        {
            case "bearer":
                return new BearerAuthConfig
                {
                    Token = settings.Secret ?? string.Empty,
                    Prefix = settings.Prefix ?? "Bearer"
                };

            case "basic":
                return new BasicAuthConfig
                {
                    Username = settings.Username ?? string.Empty,
                    Password = settings.Password ?? string.Empty
                };

            case "oauth2":
            case "oauth2-client-credentials":
            case "oauth2-authorization-code":
            case "oauth2-password":
                {
                    OAuth2GrantType grantType = ResolveOAuth2GrantType(settings);

                    var oauth2 = new OAuth2Config
                    {
                        GrantType = grantType,
                        TokenUrl = settings.TokenUrl ?? string.Empty,
                        ClientId = settings.ClientId ?? string.Empty,
                        ClientSecret = settings.ClientSecret ?? string.Empty,
                        Scope = settings.Scope ?? string.Empty
                    };

                    if (grantType == OAuth2GrantType.AuthorizationCode)
                    {
                        if (settings.AuthorizationUrl is not null)
                        {
                            oauth2.AuthorizationUrl = settings.AuthorizationUrl;
                        }

                        if (settings.RedirectUri is not null)
                        {
                            oauth2.RedirectUri = settings.RedirectUri;
                        }

                        if (settings.Pkce is not null)
                        {
                            oauth2.UsePkce = !settings.Pkce.Equals("disabled", StringComparison.OrdinalIgnoreCase);
                            oauth2.CodeChallengeMethod = settings.Pkce.Equals("plain", StringComparison.OrdinalIgnoreCase)
                                ? "plain"
                                : "S256";
                        }
                    }

                    if (grantType == OAuth2GrantType.ResourceOwnerPassword)
                    {
                        oauth2.Username = settings.Username ?? string.Empty;
                        oauth2.Password = settings.Password ?? string.Empty;
                    }

                    return oauth2;
                }

            case "custom":
                {
                    var custom = new CustomAuthConfig
                    {
                        Url = settings.CustomUrl ?? string.Empty,
                        Method = settings.CustomMethod ?? "POST"
                    };

                    foreach (string header in settings.CustomHeaders ?? [])
                    {
                        int colon = header.IndexOf(':');
                        if (colon < 0)
                        {
                            WriteError($"Invalid header (expected \"Name: Value\"): {header}", settings.Json);
                            return null;
                        }

                        custom.Headers[header[..colon].Trim()] = header[(colon + 1)..].Trim();
                    }

                    foreach (string param in settings.CustomParams ?? [])
                    {
                        int eq = param.IndexOf('=');
                        if (eq < 0)
                        {
                            WriteError($"Invalid param (expected \"key=value\"): {param}", settings.Json);
                            return null;
                        }

                        custom.Params[param[..eq]] = param[(eq + 1)..];
                    }

                    if (settings.CustomBody is not null)
                    {
                        BodyType bodyType = settings.CustomBodyType?.ToLowerInvariant() switch
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
                            WriteError(
                                $"Unknown body type: {settings.CustomBodyType!}. Use json, xml, text, form, multipart, or raw.",
                                settings.Json);
                            return null;
                        }

                        custom.BodyType = bodyType;
                        custom.Bodies[bodyType] = settings.CustomBody;
                    }

                    ExtractionSource? source = settings.ExtractionSource?.ToLowerInvariant() switch
                    {
                        "jsonpath" or "json" => ExtractionSource.JsonPath,
                        "header" => ExtractionSource.ResponseHeader,
                        "regex" => ExtractionSource.Regex,
                        _ => null
                    };

                    if (settings.ExtractionSource is not null && source is null)
                    {
                        WriteError(
                            $"Unknown extraction source: {settings.ExtractionSource}. Use jsonpath, header, or regex.",
                            settings.Json);
                        return null;
                    }

                    if (source is not null)
                    {
                        custom.Source = source.Value;
                    }

                    if (settings.ExtractionExpression is not null)
                    {
                        custom.ExtractionExpression = settings.ExtractionExpression;
                    }

                    if (settings.ApplyHeaderName is not null)
                    {
                        custom.ApplyHeaderName = settings.ApplyHeaderName;
                    }

                    if (settings.ApplyHeaderTemplate is not null)
                    {
                        custom.ApplyHeaderTemplate = settings.ApplyHeaderTemplate;
                    }

                    return custom;
                }

            default:
                WriteError(
                    $"Unknown auth type: {settings.Type}. Use bearer, basic, oauth2, oauth2-client-credentials, oauth2-authorization-code, oauth2-password, or custom.",
                    settings.Json);
                return null;
        }
    }

    private static OAuth2GrantType ResolveOAuth2GrantType(AuthCreateCommandSettings settings)
    {
        return settings.Type!.ToLowerInvariant() switch
        {
            "oauth2-client-credentials" => OAuth2GrantType.ClientCredentials,
            "oauth2-authorization-code" => OAuth2GrantType.AuthorizationCode,
            "oauth2-password" => OAuth2GrantType.ResourceOwnerPassword,
            _ => settings.GrantType?.ToLowerInvariant() switch
            {
                "client-credentials" or "client_credentials" => OAuth2GrantType.ClientCredentials,
                "authorization-code" or "authorization_code" => OAuth2GrantType.AuthorizationCode,
                "password" or "resource-owner-password" => OAuth2GrantType.ResourceOwnerPassword,
                _ => OAuth2GrantType.ClientCredentials
            }
        };
    }

    private string? PromptCreateMenu(AuthCreateStateModel state)
    {
        string nameDisplay = string.IsNullOrWhiteSpace(state.Name)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Name)}[/]";
        string authDisplay = AuthDisplayName(state.Auth);
        string autoRenewDisplay = state.AutoRenewAuth ? "[green]enabled[/]" : "[grey]disabled[/]";
        List<string> menuChoices = new()
            { ActionFinish, ActionName, ActionConfigure, ActionAutoRenew };

        if (SupportsAuthFetch(state.Auth))
        {
            menuChoices.Add(ActionFetch);
        }

        return interactiveConsole.Select("Auth setup", menuChoices,
            choice => choice switch
            {
                ActionName => $"Name: {nameDisplay}",
                ActionConfigure => $"Auth: {authDisplay}",
                ActionAutoRenew => $"Auto-renew auth: {autoRenewDisplay}",
                _ => choice
            });
    }

    private async Task<bool> TryCreateAuthAsync(AuthCreateStateModel state, StraumrWorkspaceEntry workspaceEntry)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            interactiveConsole.ShowMessage("[red]A name is required.[/]");
            return false;
        }

        if (state.Auth is null)
        {
            interactiveConsole.ShowMessage("[red]Configure an auth setup before saving.[/]");
            return false;
        }

        StraumrAuth auth = state.ToAuth();
        try
        {
            await authService.CreateAsync(workspaceEntry, auth);
            AnsiConsole.MarkupLine($"[green]Created auth[/] [bold]{auth.Name}[/] ({auth.Id})");
            return true;
        }
        catch (Exception ex)
        {
            interactiveConsole.ShowMessage($"[red]{Markup.Escape(ex.Message)}[/]");
            return false;
        }
    }

    private async Task HandleCreateActionAsync(
        AuthCreateStateModel state,
        string action,
        CancellationToken cancellationToken)
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
            case ActionConfigure:
                state.Auth = await EditAuthAsync(interactiveConsole, state.Auth);
                break;
            case ActionAutoRenew:
                state.AutoRenewAuth = !state.AutoRenewAuth;
                break;
            case ActionFetch:
                await FetchAuthValueAsync(interactiveConsole, authService, state.Auth, cancellationToken);
                break;
        }
    }
}
