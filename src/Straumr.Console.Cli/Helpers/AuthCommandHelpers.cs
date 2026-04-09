using Spectre.Console;
using Straumr.Console.Shared.Console;
using Straumr.Core.Enums;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.HttpCommandHelpers;

namespace Straumr.Console.Cli.Helpers;

internal static class AuthCommandHelpers
{
    internal static string AuthDisplayName(StraumrAuthConfig? auth)
    {
        return auth switch
        {
            null => "[grey]none[/]",
            BearerAuthConfig bearer => !string.IsNullOrWhiteSpace(bearer.Token)
                ? "[blue]Bearer[/] [green]set[/]"
                : "[blue]Bearer[/] [grey]not set[/]",
            BasicAuthConfig basic => !string.IsNullOrWhiteSpace(basic.Username)
                ? $"[blue]Basic[/] [green]{Markup.Escape(basic.Username)}[/]"
                : "[blue]Basic[/] [grey]not set[/]",
            OAuth2Config oauth2 => FormatOAuth2Display(oauth2),
            CustomAuthConfig custom => FormatCustomAuthDisplay(custom),
            _ => "[grey]none[/]"
        };
    }

    internal static string AuthTypeName(StraumrAuthConfig? auth)
    {
        return auth switch
        {
            null => "none",
            BearerAuthConfig => "Bearer",
            BasicAuthConfig => "Basic",
            OAuth2Config oauth2 => oauth2.GrantType switch
            {
                OAuth2GrantType.ClientCredentials => "OAuth2 Client Credentials",
                OAuth2GrantType.AuthorizationCode => "OAuth2 Authorization Code",
                OAuth2GrantType.ResourceOwnerPassword => "OAuth2 Password",
                _ => "OAuth2"
            },
            CustomAuthConfig => "Custom",
            _ => "none"
        };
    }

    internal static string AuthDisplayName(Guid? authId, IReadOnlyList<StraumrAuth> auths)
    {
        if (authId is null)
        {
            return "[grey]none[/]";
        }

        StraumrAuth? auth = auths.FirstOrDefault(a => a.Id == authId);
        return auth is not null
            ? $"[blue]{Markup.Escape(auth.Name)}[/]"
            : "[grey]unknown[/]";
    }

    private static string FormatCustomAuthDisplay(CustomAuthConfig config)
    {
        string sourceDisplay = config.Source switch
        {
            ExtractionSource.JsonPath => "JSON",
            ExtractionSource.ResponseHeader => "Header",
            ExtractionSource.Regex => "Regex",
            _ => config.Source.ToString()
        };
        return $"[blue]Custom ({sourceDisplay})[/]";
    }

    private static string FormatOAuth2Display(OAuth2Config config)
    {
        string grantName = config.GrantType switch
        {
            OAuth2GrantType.ClientCredentials => "Client Credentials",
            OAuth2GrantType.AuthorizationCode => "Authorization Code",
            OAuth2GrantType.ResourceOwnerPassword => "Password",
            _ => config.GrantType.ToString()
        };

        string tokenStatus = config.Token switch
        {
            null => "[grey]no token[/]",
            { IsExpired: true } => "[yellow]expired[/]",
            _ => "[green]valid[/]"
        };

        return $"[blue]OAuth 2.0 ({grantName})[/] {tokenStatus}";
    }

    internal static bool SupportsAuthFetch(StraumrAuthConfig? auth)
    {
        return auth is OAuth2Config or CustomAuthConfig;
    }

    internal static async Task<StraumrAuth?> SelectAuthAsync(
        IInteractiveConsole console,
        IStraumrAuthService authService)
    {
        IReadOnlyList<StraumrAuth> auths = await authService.ListAsync();

        const string noneOption = "None";

        List<string> choices = auths.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => a.Id.ToString())
            .Prepend(noneOption)
            .ToList();

        string? selected = await console.SelectAsync("Select auth", choices,
            choice =>
            {
                if (choice == noneOption)
                {
                    return "[grey]None[/]";
                }

                if (!Guid.TryParse(choice, out Guid id))
                {
                    return choice;
                }

                StraumrAuth? found = auths.FirstOrDefault(a => a.Id == id);
                return found is not null
                    ? $"{Markup.Escape(found.Name)} — {AuthDisplayName(found.Config)}"
                    : choice;
            });

        if (selected is null or noneOption)
        {
            return null;
        }

        if (Guid.TryParse(selected, out Guid selectedId))
        {
            return auths.FirstOrDefault(a => a.Id == selectedId);
        }

        return null;
    }

    internal static async Task<StraumrAuthConfig?> EditAuthAsync(
        IInteractiveConsole console,
        StraumrAuthConfig? auth)
    {
        StraumrAuthConfig? current = auth;
        while (true)
        {
            const string actionBack = "Back";
            const string actionType = "Auth type";
            const string actionConfig = "Configure";
            const string actionTokenStatus = "Token status";
            const string actionClear = "Clear auth";

            AuthType currentType = current?.Type ?? AuthType.None;
            string typeDisplay = currentType switch
            {
                AuthType.Bearer => "[blue]Bearer[/]",
                AuthType.Basic => "[blue]Basic[/]",
                AuthType.OAuth2 => "[blue]OAuth 2.0[/]",
                AuthType.Custom => "[blue]Custom[/]",
                _ => "[grey]none[/]"
            };

            List<string> choices = current is null
                ? [actionBack, actionType]
                : [actionBack, actionType, actionConfig, actionTokenStatus, actionClear];

            string? action = await console.SelectAsync("Auth", choices,
                choice => choice switch
                {
                    actionType => $"Type: {typeDisplay}",
                    _ => choice
                });

            if (action is null or actionBack)
            {
                return current;
            }

            switch (action)
            {
                case actionType:
                    current = await SelectAuthTypeAsync(console, current);
                    break;
                case actionConfig:
                    await ConfigureAuthAsync(console, current);
                    break;
                case actionTokenStatus:
                    ShowAuthStatus(console, current);
                    break;
                case actionClear:
                    current = null;
                    break;
            }
        }
    }

    private static async Task<StraumrAuthConfig?> SelectAuthTypeAsync(
        IInteractiveConsole console, StraumrAuthConfig? current)
    {
        string? selected = await console.SelectAsync("Select auth type",
            ["No auth", "Bearer", "Basic", "OAuth 2.0", "Custom"]);

        return selected switch
        {
            "Bearer" => current as BearerAuthConfig ?? new BearerAuthConfig(),
            "Basic" => current as BasicAuthConfig ?? new BasicAuthConfig(),
            "OAuth 2.0" => current as OAuth2Config ?? new OAuth2Config(),
            "Custom" => current as CustomAuthConfig ?? new CustomAuthConfig(),
            "No auth" => null,
            _ => current
        };
    }

    private static async Task ConfigureAuthAsync(IInteractiveConsole console, StraumrAuthConfig? auth)
    {
        switch (auth)
        {
            case BearerAuthConfig bearer:
                await EditBearerAuthAsync(console, bearer);
                break;
            case BasicAuthConfig basic:
                await EditBasicAuthAsync(console, basic);
                break;
            case OAuth2Config oauth2:
                await EditOAuth2ConfigAsync(console, oauth2);
                break;
            case CustomAuthConfig custom:
                await EditCustomAuthConfigAsync(console, custom);
                break;
        }
    }

    private static void ShowAuthStatus(IInteractiveConsole console, StraumrAuthConfig? auth)
    {
        switch (auth)
        {
            case BearerAuthConfig bearer when !string.IsNullOrWhiteSpace(bearer.Token):
                string masked = bearer.Token[..Math.Min(20, bearer.Token.Length)];
                console.ShowMessage(
                    $"Prefix: [blue]{Markup.Escape(bearer.Prefix)}[/]\nToken: [blue]{Markup.Escape(masked)}...[/]");
                break;
            case BearerAuthConfig:
                console.ShowMessage("[grey]No token set.[/]");
                break;
            case BasicAuthConfig basic when !string.IsNullOrWhiteSpace(basic.Username):
                console.ShowMessage(
                    $"Username: [blue]{Markup.Escape(basic.Username)}[/]\nPassword: [blue]****[/]");
                break;
            case BasicAuthConfig:
                console.ShowMessage("[grey]No credentials set.[/]");
                break;
            case OAuth2Config { Token: null }:
                console.ShowMessage("[grey]No token fetched yet.[/]");
                break;
            case OAuth2Config { Token: { } token }:
            {
                string status = token.IsExpired ? "[yellow]Expired[/]" : "[green]Valid[/]";
                string expiresDisplay = token.ExpiresAt.HasValue
                    ? token.ExpiresAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC")
                    : "N/A";
                string hasRefresh = token.RefreshToken is not null ? "[green]Yes[/]" : "[grey]No[/]";
                console.ShowMessage(
                    $"Status: {status}\n" +
                    $"Token type: [blue]{Markup.Escape(token.TokenType)}[/]\n" +
                    $"Expires: [blue]{expiresDisplay}[/]\n" +
                    $"Refresh token: {hasRefresh}");
                break;
            }
            case CustomAuthConfig { CachedValue: null }:
                console.ShowMessage("[grey]No value fetched yet.[/]");
                break;
            case CustomAuthConfig custom:
            {
                string headerPreview = custom.ApplyHeaderTemplate.Replace("{{value}}", custom.CachedValue);
                console.ShowMessage(
                    $"Cached value: [blue]{Markup.Escape(custom.CachedValue)}[/]\n" +
                    $"Applied as: [blue]{Markup.Escape(custom.ApplyHeaderName)}: {Markup.Escape(headerPreview)}[/]");
                break;
            }
        }
    }

    private static async Task EditBearerAuthAsync(IInteractiveConsole console, BearerAuthConfig config)
    {
        const string actionBack = "Back";
        const string actionPrefix = "Header prefix";
        const string actionToken = "Token";

        string? action = await console.SelectAsync("Bearer auth",
            [actionBack, actionPrefix, actionToken],
            choice => choice switch
            {
                actionPrefix => $"Prefix: [blue]{Markup.Escape(config.Prefix)}[/]",
                actionToken =>
                    $"Token: [blue]{(string.IsNullOrWhiteSpace(config.Token) ? "[grey]not set[/]" : "****")}[/]",
                _ => choice
            });

        if (action is null or actionBack)
        {
            return;
        }

        switch (action)
        {
            case actionPrefix:
            {
                string? prefix = await console.TextInputAsync("Header prefix", config.Prefix,
                    validate: value => string.IsNullOrWhiteSpace(value) ? "Prefix cannot be empty." : null);

                if (prefix is not null)
                {
                    config.Prefix = prefix;
                }

                break;
            }
            case actionToken:
            {
                string? token = await console.SecretInputAsync("Token");
                if (token is not null)
                {
                    config.Token = token;
                }

                break;
            }
        }
    }

    private static async Task EditBasicAuthAsync(IInteractiveConsole console, BasicAuthConfig config)
    {
        const string actionBack = "Back";
        const string actionUsername = "Username";
        const string actionPassword = "Password";

        string? action = await console.SelectAsync("Basic auth",
            [actionBack, actionUsername, actionPassword],
            choice => choice switch
            {
                actionUsername => string.IsNullOrWhiteSpace(config.Username)
                    ? "Username: [grey]not set[/]"
                    : $"Username: [blue]{Markup.Escape(config.Username)}[/]",
                actionPassword => string.IsNullOrWhiteSpace(config.Password)
                    ? "Password: [grey]not set[/]"
                    : "Password: [blue]****[/]",
                _ => choice
            });

        if (action is null or actionBack)
        {
            return;
        }

        switch (action)
        {
            case actionUsername:
            {
                string? username = await console.TextInputAsync("Username", config.Username,
                    validate: value => string.IsNullOrWhiteSpace(value) ? "Username cannot be empty." : null);
                if (username is not null)
                {
                    config.Username = username;
                }

                break;
            }
            case actionPassword:
            {
                string? password = await console.SecretInputAsync("Password");
                if (password is not null)
                {
                    config.Password = password;
                }

                break;
            }
        }
    }

    private static async Task EditOAuth2ConfigAsync(IInteractiveConsole console, OAuth2Config config)
    {
        while (true)
        {
            string grantDisplay = config.GrantType switch
            {
                OAuth2GrantType.ClientCredentials => "[blue]Client Credentials[/]",
                OAuth2GrantType.AuthorizationCode => "[blue]Authorization Code[/]",
                OAuth2GrantType.ResourceOwnerPassword => "[blue]Password[/]",
                _ => "[grey]unknown[/]"
            };

            const string actionBack = "Back";
            const string actionGrant = "Grant type";
            const string actionTokenUrl = "Token URL";
            const string actionClientId = "Client ID";
            const string actionClientSecret = "Client secret";
            const string actionScope = "Scope";
            const string actionAuthUrl = "Authorization URL";
            const string actionRedirectUri = "Redirect URI";
            const string actionPkce = "PKCE";
            const string actionUsername = "Username";
            const string actionPassword = "Password";

            string tokenUrlDisplay = string.IsNullOrWhiteSpace(config.TokenUrl)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.TokenUrl)}[/]";
            string clientIdDisplay = string.IsNullOrWhiteSpace(config.ClientId)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.ClientId)}[/]";
            string clientSecretDisplay = string.IsNullOrWhiteSpace(config.ClientSecret)
                ? "[grey]not set[/]"
                : "[blue]****[/]";
            string scopeDisplay = string.IsNullOrWhiteSpace(config.Scope)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.Scope)}[/]";

            List<string> choices = new List<string>
                { actionBack, actionGrant, actionTokenUrl, actionClientId, actionClientSecret, actionScope };

            Func<string, string> converter;

            if (config.GrantType == OAuth2GrantType.AuthorizationCode)
            {
                string authUrlDisplay = string.IsNullOrWhiteSpace(config.AuthorizationUrl)
                    ? "[grey]not set[/]"
                    : $"[blue]{Markup.Escape(config.AuthorizationUrl)}[/]";
                string redirectDisplay = $"[blue]{Markup.Escape(config.RedirectUri)}[/]";
                string pkceDisplay = config.UsePkce
                    ? $"[green]Enabled[/] ({config.CodeChallengeMethod})"
                    : "[grey]Disabled[/]";

                choices.AddRange([actionAuthUrl, actionRedirectUri, actionPkce]);

                converter = choice => choice switch
                {
                    actionGrant => $"Grant type: {grantDisplay}",
                    actionTokenUrl => $"Token URL: {tokenUrlDisplay}",
                    actionClientId => $"Client ID: {clientIdDisplay}",
                    actionClientSecret => $"Client secret: {clientSecretDisplay}",
                    actionScope => $"Scope: {scopeDisplay}",
                    actionAuthUrl => $"Authorization URL: {authUrlDisplay}",
                    actionRedirectUri => $"Redirect URI: {redirectDisplay}",
                    actionPkce => $"PKCE: {pkceDisplay}",
                    _ => choice
                };
            }
            else if (config.GrantType == OAuth2GrantType.ResourceOwnerPassword)
            {
                string usernameDisplay = string.IsNullOrWhiteSpace(config.Username)
                    ? "[grey]not set[/]"
                    : $"[blue]{Markup.Escape(config.Username)}[/]";
                string passwordDisplay = string.IsNullOrWhiteSpace(config.Password)
                    ? "[grey]not set[/]"
                    : "[blue]****[/]";

                choices.AddRange([actionUsername, actionPassword]);

                converter = choice => choice switch
                {
                    actionGrant => $"Grant type: {grantDisplay}",
                    actionTokenUrl => $"Token URL: {tokenUrlDisplay}",
                    actionClientId => $"Client ID: {clientIdDisplay}",
                    actionClientSecret => $"Client secret: {clientSecretDisplay}",
                    actionScope => $"Scope: {scopeDisplay}",
                    actionUsername => $"Username: {usernameDisplay}",
                    actionPassword => $"Password: {passwordDisplay}",
                    _ => choice
                };
            }
            else
            {
                converter = choice => choice switch
                {
                    actionGrant => $"Grant type: {grantDisplay}",
                    actionTokenUrl => $"Token URL: {tokenUrlDisplay}",
                    actionClientId => $"Client ID: {clientIdDisplay}",
                    actionClientSecret => $"Client secret: {clientSecretDisplay}",
                    actionScope => $"Scope: {scopeDisplay}",
                    _ => choice
                };
            }

            string? action = await console.SelectAsync("OAuth 2.0 Configuration", choices, converter);

            if (action is null or actionBack)
            {
                return;
            }

            await HandleOAuth2Action(console, config, action);
        }
    }

    private static async Task HandleOAuth2Action(IInteractiveConsole console, OAuth2Config config, string action)
    {
        switch (action)
        {
            case "Grant type":
            {
                string? selected = await console.SelectAsync("Select grant type",
                    ["Client Credentials", "Authorization Code", "Resource Owner Password"]);

                if (selected is not null)
                {
                    config.GrantType = selected switch
                    {
                        "Client Credentials" => OAuth2GrantType.ClientCredentials,
                        "Authorization Code" => OAuth2GrantType.AuthorizationCode,
                        "Resource Owner Password" => OAuth2GrantType.ResourceOwnerPassword,
                        _ => config.GrantType
                    };
                }

                break;
            }
            case "Token URL":
            {
                string? value = await console.TextInputAsync("Token URL", config.TokenUrl,
                    validate: v => IsValidAbsoluteUrl(v) ? null : "Enter a valid absolute URL.");
                if (value is not null)
                {
                    config.TokenUrl = value;
                }

                break;
            }
            case "Client ID":
            {
                string? value = await console.TextInputAsync("Client ID", config.ClientId,
                    validate: v => string.IsNullOrWhiteSpace(v) ? "Client ID cannot be empty." : null);
                if (value is not null)
                {
                    config.ClientId = value;
                }

                break;
            }
            case "Client secret":
            {
                string? value = await console.SecretInputAsync("Client secret");
                if (value is not null)
                {
                    config.ClientSecret = value;
                }

                break;
            }
            case "Scope":
            {
                string? value = await console.TextInputAsync("Scope (optional)", config.Scope, allowEmpty: true);
                if (value is not null)
                {
                    config.Scope = value;
                }

                break;
            }
            case "Authorization URL":
            {
                string? value = await console.TextInputAsync("Authorization URL", config.AuthorizationUrl,
                    validate: v => IsValidAbsoluteUrl(v) ? null : "Enter a valid absolute URL.");
                if (value is not null)
                {
                    config.AuthorizationUrl = value;
                }

                break;
            }
            case "Redirect URI":
            {
                string? value = await console.TextInputAsync("Redirect URI", config.RedirectUri,
                    validate: v => IsValidAbsoluteUrl(v) ? null : "Enter a valid absolute URL.");
                if (value is not null)
                {
                    config.RedirectUri = value;
                }

                break;
            }
            case "PKCE":
            {
                string? mode = await console.SelectAsync("PKCE", ["Disabled", "S256", "plain"]);
                if (mode is not null)
                {
                    config.UsePkce = mode != "Disabled";
                    config.CodeChallengeMethod = mode == "plain" ? "plain" : "S256";
                }

                break;
            }
            case "Username":
            {
                string? value = await console.TextInputAsync("Username", config.Username,
                    validate: v => string.IsNullOrWhiteSpace(v) ? "Username cannot be empty." : null);
                if (value is not null)
                {
                    config.Username = value;
                }

                break;
            }
            case "Password":
            {
                string? value = await console.SecretInputAsync("Password");
                if (value is not null)
                {
                    config.Password = value;
                }

                break;
            }
        }
    }

    private static async Task EditCustomAuthConfigAsync(IInteractiveConsole console, CustomAuthConfig config)
    {
        while (true)
        {
            const string actionBack = "Back";
            const string actionUrl = "Auth URL";
            const string actionMethod = "Request method";
            const string actionHeaders = "Headers";
            const string actionParams = "Parameters";
            const string actionBody = "Body";
            const string actionSource = "Extraction source";
            const string actionExpression = "Extraction expression";
            const string actionHeaderName = "Apply header name";
            const string actionTemplate = "Apply header template";

            string urlDisplay = string.IsNullOrWhiteSpace(config.Url)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.Url)}[/]";
            string methodDisplay = $"[blue]{config.Method}[/]";
            string headersDisplay = config.Headers.Count == 0 ? "[grey]none[/]" : $"[blue]{config.Headers.Count}[/]";
            string paramsDisplay = config.Params.Count == 0 ? "[grey]none[/]" : $"[blue]{config.Params.Count}[/]";
            string bodyDisplay = config.BodyType == BodyType.None
                ? "[grey]none[/]"
                : $"[blue]{BodyTypeDisplayName(config.BodyType)}[/]";
            string sourceDisplay = config.Source switch
            {
                ExtractionSource.JsonPath => "[blue]JSON path[/]",
                ExtractionSource.ResponseHeader => "[blue]Response header[/]",
                ExtractionSource.Regex => "[blue]Regex[/]",
                _ => "[grey]unknown[/]"
            };
            string expressionDisplay = string.IsNullOrWhiteSpace(config.ExtractionExpression)
                ? "[grey]not set[/]"
                : $"[blue]{Markup.Escape(config.ExtractionExpression)}[/]";
            string headerNameDisplay = $"[blue]{Markup.Escape(config.ApplyHeaderName)}[/]";
            string templateDisplay = $"[blue]{Markup.Escape(config.ApplyHeaderTemplate)}[/]";

            string? action = await console.SelectAsync("Custom auth",
                [
                    actionBack, actionUrl, actionMethod, actionHeaders, actionParams, actionBody,
                    actionSource, actionExpression, actionHeaderName, actionTemplate
                ],
                choice => choice switch
                {
                    actionUrl => $"Auth URL: {urlDisplay}",
                    actionMethod => $"Request method: {methodDisplay}",
                    actionHeaders => $"Headers: {headersDisplay}",
                    actionParams => $"Parameters: {paramsDisplay}",
                    actionBody => $"Body: {bodyDisplay}",
                    actionSource => $"Extraction source: {sourceDisplay}",
                    actionExpression => $"Extraction expression: {expressionDisplay}",
                    actionHeaderName => $"Header name: {headerNameDisplay}",
                    actionTemplate => $"Header template: {templateDisplay}",
                    _ => choice
                });

            if (action is null or actionBack)
            {
                return;
            }

            switch (action)
            {
                case actionUrl:
                {
                    string? url = await PromptUrlAsync(console, config.Url);
                    if (url is not null)
                    {
                        config.Url = url;
                    }

                    break;
                }
                case actionMethod:
                {
                    string? selected = await PromptMethodAsync(console);
                    if (selected is not null)
                    {
                        config.Method = selected;
                    }

                    break;
                }
                case actionHeaders:
                    await EditKeyValuePairsAsync(console, "Auth headers", config.Headers);
                    break;
                case actionParams:
                    await EditKeyValuePairsAsync(console, "Auth params", config.Params);
                    break;
                case actionBody:
                    config.BodyType = await EditBodyAsync(console, config.Headers, config.Bodies, config.BodyType,
                        CancellationToken.None);
                    break;
                case actionSource:
                {
                    string? selected = await console.SelectAsync("Extract value from",
                        ["JSON path (dot notation)", "Response header", "Regex (first capture group)"]);

                    if (selected is not null)
                    {
                        config.Source = selected switch
                        {
                            "JSON path (dot notation)" => ExtractionSource.JsonPath,
                            "Response header" => ExtractionSource.ResponseHeader,
                            "Regex (first capture group)" => ExtractionSource.Regex,
                            _ => config.Source
                        };
                    }

                    break;
                }
                case actionExpression:
                {
                    string hint = config.Source switch
                    {
                        ExtractionSource.JsonPath => "JSON path (e.g. access_token or data.token)",
                        ExtractionSource.ResponseHeader => "Header name (e.g. X-Auth-Token)",
                        ExtractionSource.Regex => "Regex with capture group (e.g. token\":\"([^\"]+)\")",
                        _ => "Expression"
                    };
                    string? value = await console.TextInputAsync(hint, config.ExtractionExpression,
                        validate: v => string.IsNullOrWhiteSpace(v) ? "Expression cannot be empty." : null);
                    if (value is not null)
                    {
                        config.ExtractionExpression = value;
                    }

                    break;
                }
                case actionHeaderName:
                {
                    string? value = await console.TextInputAsync("Header name (e.g. Authorization)",
                        config.ApplyHeaderName,
                        validate: v => string.IsNullOrWhiteSpace(v) ? "Header name cannot be empty." : null);
                    if (value is not null)
                    {
                        config.ApplyHeaderName = value;
                    }

                    break;
                }
                case actionTemplate:
                {
                    string? value = await console.TextInputAsync(
                        "Header value template (use {{value}} as placeholder)",
                        config.ApplyHeaderTemplate,
                        validate: v => string.IsNullOrWhiteSpace(v) ? "Template cannot be empty." : null);
                    if (value is not null)
                    {
                        config.ApplyHeaderTemplate = value;
                    }

                    break;
                }
            }
        }
    }

    internal static async Task FetchAuthValueAsync(
        IInteractiveConsole console, IStraumrAuthService authService, StraumrAuthConfig? auth)
    {
        if (auth is not (OAuth2Config or CustomAuthConfig))
        {
            return;
        }

        try
        {
            switch (auth)
            {
                case OAuth2Config oauth2:
                {
                    OAuth2Token token = await authService.FetchTokenAsync(oauth2);
                    oauth2.Token = token;
                    string expiresDisplay = token.ExpiresAt.HasValue
                        ? token.ExpiresAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC")
                        : "N/A";
                    console.ShowMessage(
                        $"[green]Token fetched successfully![/]\n" +
                        $"Type: [blue]{Markup.Escape(token.TokenType)}[/]\n" +
                        $"Expires: [blue]{expiresDisplay}[/]");
                    break;
                }
                case CustomAuthConfig customAuth:
                {
                    string value = await authService.ExecuteCustomAuthAsync(customAuth);
                    string headerPreview = customAuth.ApplyHeaderTemplate.Replace("{{value}}", value);
                    console.ShowMessage(
                        $"[green]Value fetched successfully![/]\n" +
                        $"Extracted: [blue]{Markup.Escape(value)}[/]\n" +
                        $"Header: [blue]{Markup.Escape(customAuth.ApplyHeaderName)}: {Markup.Escape(headerPreview)}[/]");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            console.ShowMessage($"[red]Failed to fetch: {Markup.Escape(ex.Message)}[/]");
        }
    }
}
