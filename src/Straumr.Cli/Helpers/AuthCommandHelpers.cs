using System.Text.Json;
using Spectre.Console;
using Straumr.Cli.Console;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Cli.Helpers.HttpCommandHelpers;
using static Straumr.Cli.Console.PromptHelpers;

namespace Straumr.Cli.Helpers;

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

    internal static bool SupportsAuthAutoRenew(StraumrAuthConfig? auth)
    {
        return auth is OAuth2Config or CustomAuthConfig;
    }

    private static StraumrAuthConfig? CloneAuthConfig(StraumrAuthConfig? auth)
    {
        if (auth is null)
        {
            return null;
        }

        string json = JsonSerializer.Serialize(auth, StraumrJsonContext.Default.StraumrAuthConfig);
        return JsonSerializer.Deserialize(json, StraumrJsonContext.Default.StraumrAuthConfig);
    }

    private static async Task<StraumrAuthConfig?> SelectAuthTemplateAsync(
        EscapeCancellableConsole console,
        IStraumrAuthTemplateService templateService)
    {
        IReadOnlyList<StraumrAuthTemplate> templates = await templateService.ListAsync();
        if (templates.Count == 0)
        {
            ShowTransientMessage("[yellow]No auth presets available.[/]");
            return null;
        }

        SelectionPrompt<StraumrAuthTemplate> prompt = new SelectionPrompt<StraumrAuthTemplate>()
            .Title("Select auth preset")
            .EnableSearch()
            .SearchPlaceholderText("/")
            .UseConverter(template => $"{Markup.Escape(template.Name)} — {AuthDisplayName(template.Config)}")
            .AddChoices(templates.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase));

        StraumrAuthTemplate? selected = await PromptAsync(console, prompt);
        if (selected is null)
        {
            return null;
        }

        try
        {
            StraumrAuthTemplate template = await templateService.PeekByIdAsync(selected.Id);
            return CloneAuthConfig(template.Config);
        }
        catch (Exception ex)
        {
            ShowTransientMessage($"[red]Failed to load preset: {Markup.Escape(ex.Message)}[/]");
            return null;
        }
    }

    internal static async Task<StraumrAuthConfig?> EditAuthAsync(
        EscapeCancellableConsole console,
        StraumrAuthConfig? auth,
        IStraumrAuthTemplateService? templateService = null)
    {
        StraumrAuthConfig? current = auth;
        while (true)
        {
            const string actionBack = "Back";
            const string actionType = "Auth type";
            const string actionConfig = "Configure";
            const string actionTokenStatus = "Token status";
            const string actionClear = "Clear auth";
            const string actionApplyPreset = "Apply preset";

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
                ? new List<string> { actionBack, actionType }
                : new List<string> { actionBack, actionType, actionConfig, actionTokenStatus, actionClear };

            if (templateService is not null)
            {
                choices.Insert(2, actionApplyPreset);
            }

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Auth")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
                {
                    actionType => $"Type: {typeDisplay}",
                    _ => choice
                })
                .AddChoices(choices);

            string? action = await PromptAsync(console, prompt);
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
                    ShowAuthStatus(current);
                    break;
                case actionClear:
                    current = null;
                    break;
                case actionApplyPreset when templateService is not null:
                {
                    StraumrAuthConfig? preset = await SelectAuthTemplateAsync(console, templateService);
                    if (preset is not null)
                    {
                        current = preset;
                    }

                    break;
                }
            }
        }
    }

    private static async Task<StraumrAuthConfig?> SelectAuthTypeAsync(
        EscapeCancellableConsole console, StraumrAuthConfig? current)
    {
        string? selected = await PromptMenuAsync(console, "Select auth type",
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

    private static async Task ConfigureAuthAsync(EscapeCancellableConsole console, StraumrAuthConfig? auth)
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

    private static void ShowAuthStatus(StraumrAuthConfig? auth)
    {
        switch (auth)
        {
            case BearerAuthConfig bearer when !string.IsNullOrWhiteSpace(bearer.Token):
                string masked = bearer.Token[..Math.Min(20, bearer.Token.Length)];
                ShowTransientMessage(
                    $"Prefix: [blue]{Markup.Escape(bearer.Prefix)}[/]\nToken: [blue]{Markup.Escape(masked)}...[/]");
                break;
            case BearerAuthConfig:
                ShowTransientMessage("[grey]No token set.[/]");
                break;
            case BasicAuthConfig basic when !string.IsNullOrWhiteSpace(basic.Username):
                ShowTransientMessage(
                    $"Username: [blue]{Markup.Escape(basic.Username)}[/]\nPassword: [blue]****[/]");
                break;
            case BasicAuthConfig:
                ShowTransientMessage("[grey]No credentials set.[/]");
                break;
            case OAuth2Config { Token: null }:
                ShowTransientMessage("[grey]No token fetched yet.[/]");
                break;
            case OAuth2Config { Token: { } token }:
            {
                string status = token.IsExpired ? "[yellow]Expired[/]" : "[green]Valid[/]";
                string expiresDisplay = token.ExpiresAt.HasValue
                    ? token.ExpiresAt.Value.ToString("yyyy-MM-dd HH:mm:ss UTC")
                    : "N/A";
                string hasRefresh = token.RefreshToken is not null ? "[green]Yes[/]" : "[grey]No[/]";
                ShowTransientMessage(
                    $"Status: {status}\n" +
                    $"Token type: [blue]{Markup.Escape(token.TokenType)}[/]\n" +
                    $"Expires: [blue]{expiresDisplay}[/]\n" +
                    $"Refresh token: {hasRefresh}");
                break;
            }
            case CustomAuthConfig { CachedValue: null }:
                ShowTransientMessage("[grey]No value fetched yet.[/]");
                break;
            case CustomAuthConfig custom:
            {
                string headerPreview = custom.ApplyHeaderTemplate.Replace("{{value}}", custom.CachedValue);
                ShowTransientMessage(
                    $"Cached value: [blue]{Markup.Escape(custom.CachedValue)}[/]\n" +
                    $"Applied as: [blue]{Markup.Escape(custom.ApplyHeaderName)}: {Markup.Escape(headerPreview)}[/]");
                break;
            }
        }
    }

    private static async Task EditBearerAuthAsync(EscapeCancellableConsole console, BearerAuthConfig config)
    {
        const string actionBack = "Back";
        const string actionPrefix = "Header prefix";
        const string actionToken = "Token";

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Bearer auth")
            .EnableSearch()
            .SearchPlaceholderText("/")
            .UseConverter(choice => choice switch
            {
                actionPrefix => $"Prefix: [blue]{Markup.Escape(config.Prefix)}[/]",
                actionToken =>
                    $"Token: [blue]{(string.IsNullOrWhiteSpace(config.Token) ? "[grey]not set[/]" : "****")}[/]",
                _ => choice
            })
            .AddChoices(actionBack, actionPrefix, actionToken);

        string? action = await PromptAsync(console, prompt);
        if (action is null or actionBack)
        {
            return;
        }

        switch (action)
        {
            case actionPrefix:
            {
                TextPrompt<string> prefixPrompt = new TextPrompt<string>("Header prefix")
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("Prefix cannot be empty.")
                        : ValidationResult.Success());
                string? prefix = await PromptTextAsync(console, prefixPrompt, config.Prefix);

                if (prefix is not null)
                {
                    config.Prefix = prefix;
                }

                break;
            }
            case actionToken:
            {
                string? token = await PromptAsync(console,
                    new TextPrompt<string>("Token")
                        .Secret());
                if (token is not null)
                {
                    config.Token = token;
                }

                break;
            }
        }
    }

    private static async Task EditBasicAuthAsync(EscapeCancellableConsole console, BasicAuthConfig config)
    {
        const string actionBack = "Back";
        const string actionUsername = "Username";
        const string actionPassword = "Password";

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title("Basic auth")
            .EnableSearch()
            .SearchPlaceholderText("/")
            .UseConverter(choice => choice switch
            {
                actionUsername => string.IsNullOrWhiteSpace(config.Username)
                    ? "Username: [grey]not set[/]"
                    : $"Username: [blue]{Markup.Escape(config.Username)}[/]",
                actionPassword => string.IsNullOrWhiteSpace(config.Password)
                    ? "Password: [grey]not set[/]"
                    : "Password: [blue]****[/]",
                _ => choice
            })
            .AddChoices(actionBack, actionUsername, actionPassword);

        string? action = await PromptAsync(console, prompt);
        if (action is null or actionBack)
        {
            return;
        }

        switch (action)
        {
            case actionUsername:
            {
                TextPrompt<string> usernamePrompt = new TextPrompt<string>("Username")
                    .Validate(value => string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("Username cannot be empty.")
                        : ValidationResult.Success());
                string? username = await PromptTextAsync(console, usernamePrompt, config.Username);
                if (username is not null)
                {
                    config.Username = username;
                }

                break;
            }
            case actionPassword:
            {
                string? password = await PromptAsync(console,
                    new TextPrompt<string>("Password")
                        .Secret());
                if (password is not null)
                {
                    config.Password = password;
                }

                break;
            }
        }
    }

    private static async Task EditOAuth2ConfigAsync(EscapeCancellableConsole console, OAuth2Config config)
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

            var choices = new List<string>
                { actionBack, actionGrant, actionTokenUrl, actionClientId, actionClientSecret, actionScope };

            if (config.GrantType == OAuth2GrantType.AuthorizationCode)
            {
                string authUrlDisplay = string.IsNullOrWhiteSpace(config.AuthorizationUrl)
                    ? "[grey]not set[/]"
                    : $"[blue]{Markup.Escape(config.AuthorizationUrl)}[/]";
                var redirectDisplay = $"[blue]{Markup.Escape(config.RedirectUri)}[/]";
                string pkceDisplay = config.UsePkce
                    ? $"[green]Enabled[/] ({config.CodeChallengeMethod})"
                    : "[grey]Disabled[/]";

                choices.AddRange([actionAuthUrl, actionRedirectUri, actionPkce]);

                SelectionPrompt<string> authCodePrompt = new SelectionPrompt<string>()
                    .Title("OAuth 2.0 Configuration")
                    .EnableSearch()
                    .SearchPlaceholderText("/")
                    .UseConverter(choice => choice switch
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
                    })
                    .AddChoices(choices);

                string? action = await PromptAsync(console, authCodePrompt);
                if (action is null or actionBack)
                {
                    return;
                }

                await HandleOAuth2Action(console, config, action);
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

                SelectionPrompt<string> passwordPrompt = new SelectionPrompt<string>()
                    .Title("OAuth 2.0 Configuration")
                    .EnableSearch()
                    .SearchPlaceholderText("/")
                    .UseConverter(choice => choice switch
                    {
                        actionGrant => $"Grant type: {grantDisplay}",
                        actionTokenUrl => $"Token URL: {tokenUrlDisplay}",
                        actionClientId => $"Client ID: {clientIdDisplay}",
                        actionClientSecret => $"Client secret: {clientSecretDisplay}",
                        actionScope => $"Scope: {scopeDisplay}",
                        actionUsername => $"Username: {usernameDisplay}",
                        actionPassword => $"Password: {passwordDisplay}",
                        _ => choice
                    })
                    .AddChoices(choices);

                string? action = await PromptAsync(console, passwordPrompt);
                if (action is null or actionBack)
                {
                    return;
                }

                await HandleOAuth2Action(console, config, action);
            }
            else
            {
                SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                    .Title("OAuth 2.0 Configuration")
                    .EnableSearch()
                    .SearchPlaceholderText("/")
                    .UseConverter(choice => choice switch
                    {
                        actionGrant => $"Grant type: {grantDisplay}",
                        actionTokenUrl => $"Token URL: {tokenUrlDisplay}",
                        actionClientId => $"Client ID: {clientIdDisplay}",
                        actionClientSecret => $"Client secret: {clientSecretDisplay}",
                        actionScope => $"Scope: {scopeDisplay}",
                        _ => choice
                    })
                    .AddChoices(choices);

                string? action = await PromptAsync(console, prompt);
                if (action is null or actionBack)
                {
                    return;
                }

                await HandleOAuth2Action(console, config, action);
            }
        }
    }

    private static async Task HandleOAuth2Action(EscapeCancellableConsole console, OAuth2Config config, string action)
    {
        switch (action)
        {
            case "Grant type":
            {
                string? selected = await PromptMenuAsync(console, "Select grant type",
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
                TextPrompt<string> valuePrompt = new TextPrompt<string>("Token URL")
                    .Validate(v => Uri.TryCreate(v, UriKind.Absolute, out _)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Enter a valid absolute URL."));
                string? value = await PromptTextAsync(console, valuePrompt, config.TokenUrl);
                if (value is not null)
                {
                    config.TokenUrl = value;
                }

                break;
            }
            case "Client ID":
            {
                TextPrompt<string> valuePrompt = new TextPrompt<string>("Client ID")
                    .Validate(v => string.IsNullOrWhiteSpace(v)
                        ? ValidationResult.Error("Client ID cannot be empty.")
                        : ValidationResult.Success());
                string? value = await PromptTextAsync(console, valuePrompt, config.ClientId);
                if (value is not null)
                {
                    config.ClientId = value;
                }

                break;
            }
            case "Client secret":
            {
                string? value = await PromptAsync(console,
                    new TextPrompt<string>("Client secret")
                        .Secret());
                if (value is not null)
                {
                    config.ClientSecret = value;
                }

                break;
            }
            case "Scope":
            {
                TextPrompt<string> valuePrompt = new TextPrompt<string>("Scope (optional)")
                    .AllowEmpty();
                string? value = await PromptTextAsync(console, valuePrompt, config.Scope);
                if (value is not null)
                {
                    config.Scope = value;
                }

                break;
            }
            case "Authorization URL":
            {
                TextPrompt<string> valuePrompt = new TextPrompt<string>("Authorization URL")
                    .Validate(v => Uri.TryCreate(v, UriKind.Absolute, out _)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Enter a valid absolute URL."));
                string? value = await PromptTextAsync(console, valuePrompt, config.AuthorizationUrl);
                if (value is not null)
                {
                    config.AuthorizationUrl = value;
                }

                break;
            }
            case "Redirect URI":
            {
                TextPrompt<string> valuePrompt = new TextPrompt<string>("Redirect URI")
                    .Validate(v => Uri.TryCreate(v, UriKind.Absolute, out _)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Enter a valid absolute URL."));
                string? value = await PromptTextAsync(console, valuePrompt, config.RedirectUri);
                if (value is not null)
                {
                    config.RedirectUri = value;
                }

                break;
            }
            case "PKCE":
            {
                string? mode = await PromptMenuAsync(console, "PKCE",
                    ["Disabled", "S256", "plain"]);
                if (mode is not null)
                {
                    config.UsePkce = mode != "Disabled";
                    config.CodeChallengeMethod = mode == "plain" ? "plain" : "S256";
                }

                break;
            }
            case "Username":
            {
                TextPrompt<string> valuePrompt = new TextPrompt<string>("Username")
                    .Validate(v => string.IsNullOrWhiteSpace(v)
                        ? ValidationResult.Error("Username cannot be empty.")
                        : ValidationResult.Success());
                string? value = await PromptTextAsync(console, valuePrompt, config.Username);
                if (value is not null)
                {
                    config.Username = value;
                }

                break;
            }
            case "Password":
            {
                string? value = await PromptAsync(console,
                    new TextPrompt<string>("Password")
                        .Secret());
                if (value is not null)
                {
                    config.Password = value;
                }

                break;
            }
        }
    }

    private static async Task EditCustomAuthConfigAsync(EscapeCancellableConsole console, CustomAuthConfig config)
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
            var methodDisplay = $"[blue]{config.Method}[/]";
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
            var headerNameDisplay = $"[blue]{Markup.Escape(config.ApplyHeaderName)}[/]";
            var templateDisplay = $"[blue]{Markup.Escape(config.ApplyHeaderTemplate)}[/]";

            SelectionPrompt<string> prompt = new SelectionPrompt<string>()
                .Title("Custom auth")
                .EnableSearch()
                .SearchPlaceholderText("/")
                .UseConverter(choice => choice switch
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
                })
                .AddChoices(
                    actionBack,
                    actionUrl,
                    actionMethod,
                    actionHeaders,
                    actionParams,
                    actionBody,
                    actionSource,
                    actionExpression,
                    actionHeaderName,
                    actionTemplate);

            string? action = await PromptAsync(console, prompt);
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
                    string? selected = await PromptMenuAsync(console, "Extract value from",
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
                    TextPrompt<string> expressionPrompt = new TextPrompt<string>(hint)
                        .Validate(v => string.IsNullOrWhiteSpace(v)
                            ? ValidationResult.Error("Expression cannot be empty.")
                            : ValidationResult.Success());
                    string? value = await PromptTextAsync(console, expressionPrompt, config.ExtractionExpression);
                    if (value is not null)
                    {
                        config.ExtractionExpression = value;
                    }

                    break;
                }
                case actionHeaderName:
                {
                    TextPrompt<string> headerPrompt = new TextPrompt<string>("Header name (e.g. Authorization)")
                        .Validate(v => string.IsNullOrWhiteSpace(v)
                            ? ValidationResult.Error("Header name cannot be empty.")
                            : ValidationResult.Success());
                    string? value = await PromptTextAsync(console, headerPrompt, config.ApplyHeaderName);
                    if (value is not null)
                    {
                        config.ApplyHeaderName = value;
                    }

                    break;
                }
                case actionTemplate:
                {
                    TextPrompt<string> templatePrompt =
                        new TextPrompt<string>("Header value template (use {{value}} as placeholder)")
                            .Validate(v => string.IsNullOrWhiteSpace(v)
                                ? ValidationResult.Error("Template cannot be empty.")
                                : ValidationResult.Success());
                    string? value = await PromptTextAsync(console, templatePrompt, config.ApplyHeaderTemplate);
                    if (value is not null)
                    {
                        config.ApplyHeaderTemplate = value;
                    }

                    break;
                }
            }
        }
    }

    internal static async Task FetchAuthValueAsync(IStraumrAuthService authService, StraumrAuthConfig? auth)
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
                    ShowTransientMessage(
                        $"[green]Token fetched successfully![/]\n" +
                        $"Type: [blue]{Markup.Escape(token.TokenType)}[/]\n" +
                        $"Expires: [blue]{expiresDisplay}[/]");
                    break;
                }
                case CustomAuthConfig customAuth:
                {
                    string value = await authService.ExecuteCustomAuthAsync(customAuth);
                    string headerPreview = customAuth.ApplyHeaderTemplate.Replace("{{value}}", value);
                    ShowTransientMessage(
                        $"[green]Value fetched successfully![/]\n" +
                        $"Extracted: [blue]{Markup.Escape(value)}[/]\n" +
                        $"Header: [blue]{Markup.Escape(customAuth.ApplyHeaderName)}: {Markup.Escape(headerPreview)}[/]");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ShowTransientMessage($"[red]Failed to fetch: {Markup.Escape(ex.Message)}[/]");
        }
    }
}