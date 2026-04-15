using Straumr.Console.Tui.Console;
using Straumr.Console.Tui.Helpers;
using Straumr.Console.Tui.Services.Interfaces;
using Straumr.Core.Enums;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Console.Tui.Services;

public sealed class AuthEditor(
    TuiInteractiveConsole interactiveConsole,
    IStraumrAuthService authService,
    IBodyEditor bodyEditor,
    ITuiOperationExecutor executor) : IAuthEditor
{
    private const string ActionFinish = "Finish";
    private const string ActionSave = "Save";
    private const string ActionName = "Edit name";
    private const string ActionConfigure = "Configure auth";
    private const string ActionAutoRenew = "Toggle auto-renew";
    private const string ActionFetch = "Fetch token/value";

    public void Run(AuthEditorContext context)
    {
        AuthEditorState state = context.Mode == AuthEditorMode.Create
            ? AuthEditorState.CreateNew()
            : AuthEditorState.FromAuth(context.ExistingAuth ?? throw new InvalidOperationException("Auth required."));

        RunEditor(state, context);
    }

    private void RunEditor(AuthEditorState state, AuthEditorContext context)
    {
        string completionAction = context.Mode == AuthEditorMode.Create ? ActionFinish : ActionSave;
        string promptTitle = context.Mode == AuthEditorMode.Create ? "Create auth" : "Edit auth";

        while (true)
        {
            string? action = interactiveConsole.Select(
                promptTitle,
                BuildChoices(completionAction, state),
                choice => DescribeChoice(choice, state, completionAction));

            if (action is null)
            {
                return;
            }

            if (action == completionAction)
            {
                if (TryPersistAuth(state, context))
                {
                    return;
                }

                continue;
            }

            HandleAction(action, state);
        }
    }

    private static IReadOnlyList<string> BuildChoices(string completionAction, AuthEditorState state)
    {
        List<string> choices = [completionAction, ActionName, ActionConfigure, ActionAutoRenew];
        if (AuthDisplayFormatter.SupportsAuthFetch(state.Config))
        {
            choices.Add(ActionFetch);
        }

        return choices;
    }

    private static string DescribeChoice(string action, AuthEditorState state, string completionAction)
    {
        return action switch
        {
            var value when value == completionAction => completionAction,
            ActionName => $"Name: {FormatAuthName(state.Name)}",
            ActionConfigure => $"Auth: {AuthDisplayFormatter.DescribeAuth(state.Config)}",
            ActionAutoRenew => $"Auto-renew auth: {(state.AutoRenew ? "[success]enabled[/]" : "[secondary]disabled[/]")}",
            _ => action
        };
    }

    private static string FormatAuthName(string name)
        => string.IsNullOrWhiteSpace(name) ? "[secondary]not set[/]" : $"[blue]{name}[/]";

    private bool TryPersistAuth(AuthEditorState state, AuthEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(state.Name))
        {
            interactiveConsole.ShowMessage("Validation", "A name is required.");
            return false;
        }

        if (state.Config is null)
        {
            interactiveConsole.ShowMessage("Validation", "Configure an auth definition before saving.");
            return false;
        }

        if (context.Mode == AuthEditorMode.Create)
        {
            StraumrAuth auth = new()
            {
                Name = state.Name,
                Config = state.CloneConfig(),
                AutoRenewAuth = state.AutoRenew
            };

            if (!executor.TryExecute(
                    () => authService.CreateAsync(auth, context.WorkspaceEntry).GetAwaiter().GetResult(),
                    context.ShowDanger))
            {
                return false;
            }

            _ = context.RefreshEntries();
            context.ShowSuccess($"Created auth \"{auth.Name}\"");
            return true;
        }

        StraumrAuth existing = context.ExistingAuth ?? throw new InvalidOperationException("Auth required.");
        existing.Name = state.Name;
        existing.Config = state.CloneConfig();
        existing.AutoRenewAuth = state.AutoRenew;

        if (!executor.TryExecute(
                () => authService.UpdateAsync(existing, context.WorkspaceEntry).GetAwaiter().GetResult(),
                context.ShowDanger))
        {
            return false;
        }

        _ = context.RefreshEntries();
        context.ShowSuccess($"Updated auth \"{existing.Name}\"");
        return true;
    }

    private void HandleAction(string action, AuthEditorState state)
    {
        switch (action)
        {
            case ActionName:
            {
                string? updated = interactiveConsole.TextInput(
                    "Name",
                    state.Name,
                    validate: value => string.IsNullOrWhiteSpace(value) ? "Name cannot be empty." : null);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    state.Name = updated;
                }

                break;
            }
            case ActionConfigure:
                ConfigureAuth(state);
                break;
            case ActionAutoRenew:
                state.AutoRenew = !state.AutoRenew;
                break;
            case ActionFetch:
                FetchAuthValue(state);
                break;
        }
    }

    private void ConfigureAuth(AuthEditorState state)
    {
        const string bearerOption = "Bearer";
        const string basicOption = "Basic";
        const string oauthOption = "OAuth 2.0";
        const string customOption = "Custom";
        const string noneOption = "No auth";

        string current = state.Config switch
        {
            BearerAuthConfig => bearerOption,
            BasicAuthConfig => basicOption,
            OAuth2Config => oauthOption,
            CustomAuthConfig => customOption,
            _ => noneOption
        };

        string? selected = interactiveConsole.Select(
            "Auth type",
            [bearerOption, basicOption, oauthOption, customOption, noneOption],
            choice => choice == current ? $"{choice} [accent](current)[/]" : choice);

        if (selected is null)
        {
            return;
        }

        state.Config = selected switch
        {
            bearerOption => state.Config as BearerAuthConfig ?? new BearerAuthConfig(),
            basicOption => state.Config as BasicAuthConfig ?? new BasicAuthConfig(),
            oauthOption => state.Config as OAuth2Config ?? new OAuth2Config(),
            customOption => state.Config as CustomAuthConfig ?? new CustomAuthConfig(),
            _ => null
        };

        switch (state.Config)
        {
            case BearerAuthConfig bearer:
                EditBearerAuth(bearer);
                break;
            case BasicAuthConfig basic:
                EditBasicAuth(basic);
                break;
            case OAuth2Config oauth2:
                EditOAuth2Config(oauth2);
                break;
            case CustomAuthConfig custom:
                EditCustomAuthConfig(custom);
                break;
        }
    }

    private void EditBearerAuth(BearerAuthConfig config)
    {
        while (true)
        {
            string? action = interactiveConsole.Select(
                "Bearer auth",
                ["Back", "Header prefix", "Token"],
                choice => choice switch
                {
                    "Header prefix" => $"Prefix: [blue]{config.Prefix}[/]",
                    "Token" => string.IsNullOrWhiteSpace(config.Token)
                        ? "Token: [secondary]not set[/]"
                        : "Token: [success]set[/]",
                    _ => choice
                });

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Header prefix":
                {
                    string? prefix = interactiveConsole.TextInput(
                        "Header prefix",
                        config.Prefix,
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Prefix cannot be empty." : null);
                    if (prefix is not null)
                    {
                        config.Prefix = prefix;
                    }

                    break;
                }
                case "Token":
                {
                    string? token = interactiveConsole.SecretInput("Token");
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        config.Token = token;
                    }

                    break;
                }
            }
        }
    }

    private void EditBasicAuth(BasicAuthConfig config)
    {
        while (true)
        {
            string? action = interactiveConsole.Select(
                "Basic auth",
                ["Back", "Username", "Password"],
                choice => choice switch
                {
                    "Username" => string.IsNullOrWhiteSpace(config.Username)
                        ? "Username: [secondary]not set[/]"
                        : $"Username: [blue]{config.Username}[/]",
                    "Password" => string.IsNullOrWhiteSpace(config.Password)
                        ? "Password: [secondary]not set[/]"
                        : "Password: [success]set[/]",
                    _ => choice
                });

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Username":
                {
                    string? username = interactiveConsole.TextInput(
                        "Username",
                        config.Username,
                        validate: value => string.IsNullOrWhiteSpace(value) ? "Username cannot be empty." : null);
                    if (username is not null)
                    {
                        config.Username = username;
                    }

                    break;
                }
                case "Password":
                {
                    string? password = interactiveConsole.SecretInput("Password");
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        config.Password = password;
                    }

                    break;
                }
            }
        }
    }

    private void EditOAuth2Config(OAuth2Config config)
    {
        while (true)
        {
            List<string> choices =
            [
                "Back",
                "Grant type",
                "Token URL",
                "Client ID",
                "Client secret",
                "Scope",
                "Cached token"
            ];
            Func<string, string> converter = choice => choice switch
            {
                "Grant type" => $"Grant type: [blue]{FormatGrant(config.GrantType)}[/]",
                "Token URL" => $"Token URL: {(string.IsNullOrWhiteSpace(config.TokenUrl) ? "[secondary]not set[/]" : $"[blue]{config.TokenUrl}[/]")}",
                "Client ID" => $"Client ID: {(string.IsNullOrWhiteSpace(config.ClientId) ? "[secondary]not set[/]" : $"[blue]{config.ClientId}[/]")}",
                "Client secret" => $"Client secret: {(string.IsNullOrWhiteSpace(config.ClientSecret) ? "[secondary]not set[/]" : "[success]set[/]")}",
                "Scope" => $"Scope: {(string.IsNullOrWhiteSpace(config.Scope) ? "[secondary]not set[/]" : $"[blue]{config.Scope}[/]")}",
                "Cached token" => config.Token is null
                    ? "Cached token: [secondary]not set[/]"
                    : "Cached token: [success]set[/]",
                _ => choice
            };

            if (config.GrantType == OAuth2GrantType.AuthorizationCode)
            {
                choices.AddRange(["Authorization URL", "Redirect URI", "PKCE"]);
                Func<string, string> baseConverter = converter;
                converter = choice => choice switch
                {
                    "Authorization URL" => $"Authorization URL: {(string.IsNullOrWhiteSpace(config.AuthorizationUrl) ? "[secondary]not set[/]" : $"[blue]{config.AuthorizationUrl}[/]")}",
                    "Redirect URI" => $"Redirect URI: [blue]{config.RedirectUri}[/]",
                    "PKCE" => $"PKCE: {(config.UsePkce ? $"[success]Enabled[/] ({config.CodeChallengeMethod})" : "[secondary]Disabled[/]")}",
                    _ => baseConverter(choice)
                };
            }
            else if (config.GrantType == OAuth2GrantType.ResourceOwnerPassword)
            {
                choices.AddRange(["Username", "Password"]);
                Func<string, string> baseConverter = converter;
                converter = choice => choice switch
                {
                    "Username" => $"Username: {(string.IsNullOrWhiteSpace(config.Username) ? "[secondary]not set[/]" : $"[blue]{config.Username}[/]")}",
                    "Password" => $"Password: {(string.IsNullOrWhiteSpace(config.Password) ? "[secondary]not set[/]" : "[success]set[/]")}",
                    _ => baseConverter(choice)
                };
            }

            string? action = interactiveConsole.Select("OAuth 2.0 configuration", choices, converter);
            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Grant type":
                    config.GrantType = PromptGrantType(config.GrantType);
                    break;
                case "Token URL":
                {
                    string? url = PromptUrl(config.TokenUrl);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        config.TokenUrl = NormalizeUrlInput(url);
                    }

                    break;
                }
                case "Client ID":
                {
                    string? clientId = interactiveConsole.TextInput("Client ID", config.ClientId);
                    if (clientId is not null)
                    {
                        config.ClientId = clientId;
                    }

                    break;
                }
                case "Client secret":
                {
                    string? secret = interactiveConsole.SecretInput("Client secret");
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        config.ClientSecret = secret;
                    }

                    break;
                }
                case "Scope":
                {
                    string? scope = interactiveConsole.TextInput("Scope", config.Scope ?? string.Empty, allowEmpty: true);
                    if (scope is not null)
                    {
                        config.Scope = scope;
                    }

                    break;
                }
                case "Authorization URL":
                {
                    string? url = PromptUrl(config.AuthorizationUrl);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        config.AuthorizationUrl = NormalizeUrlInput(url);
                    }

                    break;
                }
                case "Redirect URI":
                {
                    string? redirect = interactiveConsole.TextInput(
                        "Redirect URI",
                        config.RedirectUri,
                        allowEmpty: false,
                        validate: value => IsValidAbsoluteUrl(NormalizeUrlInput(value))
                            ? null
                            : "Enter a valid absolute URL.");
                    if (redirect is not null)
                    {
                        config.RedirectUri = NormalizeUrlInput(redirect);
                    }

                    break;
                }
                case "PKCE":
                {
                    string? pkce = interactiveConsole.Select("PKCE",
                        ["Disabled", "S256", "Plain"],
                        choice => choice == "Disabled"
                            ? "[secondary]Disabled[/]"
                            : $"[blue]{choice}[/]");
                    if (pkce is not null)
                    {
                        config.UsePkce = pkce != "Disabled";
                        config.CodeChallengeMethod = pkce switch
                        {
                            "Plain" => "plain",
                            _ => "S256"
                        };
                    }

                    break;
                }
                case "Username":
                {
                    string? username = interactiveConsole.TextInput("Username", config.Username);
                    if (username is not null)
                    {
                        config.Username = username;
                    }

                    break;
                }
                case "Password":
                {
                    string? password = interactiveConsole.SecretInput("Password");
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        config.Password = password;
                    }

                    break;
                }
                case "Cached token":
                {
                    string? confirm = interactiveConsole.Select(
                        "Cached token",
                        ["Back", "Clear token"],
                        choice => choice switch
                        {
                            "Clear token" => config.Token is null
                                ? "Clear token [secondary](no token cached)[/]"
                                : "Clear token [danger](will remove cached token)[/]",
                            _ => choice
                        });
                    if (confirm == "Clear token")
                    {
                        config.Token = null;
                    }

                    break;
                }
            }
        }
    }

    private void EditCustomAuthConfig(CustomAuthConfig config)
    {
        while (true)
        {
            string? action = interactiveConsole.Select(
                "Custom auth",
                [
                    "Back",
                    "HTTP method",
                    "URL",
                    "Headers",
                    "Params",
                    "Body",
                    "Source",
                    "Extraction expression",
                    "Apply header name",
                    "Apply header template",
                    "Cached value"
                ],
                choice => choice switch
                {
                    "HTTP method" => $"HTTP method: [blue]{config.Method}[/]",
                    "URL" => $"URL: {(string.IsNullOrWhiteSpace(config.Url) ? "[secondary]not set[/]" : $"[blue]{config.Url}[/]")}",
                    "Headers" => $"Headers: [blue]{config.Headers.Count}[/]",
                    "Params" => $"Params: [blue]{config.Params.Count}[/]",
                    "Body" => $"Body: [blue]{config.BodyType}[/]",
                    "Source" => $"Source: [blue]{config.Source}[/]",
                    "Extraction expression" => string.IsNullOrWhiteSpace(config.ExtractionExpression)
                        ? "Extraction expression: [secondary]not set[/]"
                        : "Extraction expression: [success]set[/]",
                    "Apply header name" => string.IsNullOrWhiteSpace(config.ApplyHeaderName)
                        ? "Apply header name: [secondary]not set[/]"
                        : $"Apply header name: [blue]{config.ApplyHeaderName}[/]",
                    "Apply header template" => string.IsNullOrWhiteSpace(config.ApplyHeaderTemplate)
                        ? "Apply header template: [secondary]not set[/]"
                        : "Apply header template: [success]set[/]",
                    "Cached value" => string.IsNullOrWhiteSpace(config.CachedValue)
                        ? "Cached value: [secondary]not set[/]"
                        : "Cached value: [success]set[/]",
                    _ => choice
                });

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "HTTP method":
                {
                    string? method = PromptMethod(config.Method);
                    if (method is not null)
                    {
                        config.Method = method;
                    }

                    break;
                }
                case "URL":
                {
                    string? url = PromptUrl(config.Url);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        config.Url = NormalizeUrlInput(url);
                    }

                    break;
                }
                case "Headers":
                    EditDictionary(config.Headers, "Header name", "Header value");
                    break;
                case "Params":
                    EditDictionary(config.Params, "Param name", "Param value");
                    break;
                case "Body":
                    config.BodyType = bodyEditor.Edit(config.Headers, config.Bodies, config.BodyType);
                    break;
                case "Source":
                    config.Source = PromptExtractionSource(config.Source);
                    break;
                case "Extraction expression":
                {
                    string? expression = interactiveConsole.TextInput(
                        "Extraction expression",
                        config.ExtractionExpression,
                        allowEmpty: true);
                    if (expression is not null)
                    {
                        config.ExtractionExpression = expression;
                    }

                    break;
                }
                case "Apply header name":
                {
                    string? header = interactiveConsole.TextInput(
                        "Apply header name",
                        config.ApplyHeaderName,
                        allowEmpty: true);
                    if (header is not null)
                    {
                        config.ApplyHeaderName = header;
                    }

                    break;
                }
                case "Apply header template":
                {
                    string? template = interactiveConsole.TextInput(
                        "Apply header template",
                        config.ApplyHeaderTemplate,
                        allowEmpty: true);
                    if (template is not null)
                    {
                        config.ApplyHeaderTemplate = template;
                    }

                    break;
                }
                case "Cached value":
                {
                    string? confirm = interactiveConsole.Select(
                        "Cached value",
                        ["Back", "Clear cached value"],
                        choice => choice switch
                        {
                            "Clear cached value" => "Clear cached value",
                            _ => choice
                        });
                    if (confirm == "Clear cached value")
                    {
                        config.CachedValue = null;
                    }

                    break;
                }
            }
        }
    }

    private OAuth2GrantType PromptGrantType(OAuth2GrantType current)
    {
        string currentDisplay = FormatGrant(current);
        string? selected = interactiveConsole.Select(
            "Grant type",
            ["Client Credentials", "Authorization Code", "Password"],
            choice => choice == currentDisplay ? $"{choice} [accent](current)[/]" : choice);

        return selected switch
        {
            "Authorization Code" => OAuth2GrantType.AuthorizationCode,
            "Password" => OAuth2GrantType.ResourceOwnerPassword,
            _ => OAuth2GrantType.ClientCredentials
        };
    }

    private static string FormatGrant(OAuth2GrantType grantType)
    {
        return grantType switch
        {
            OAuth2GrantType.ClientCredentials => "Client Credentials",
            OAuth2GrantType.AuthorizationCode => "Authorization Code",
            OAuth2GrantType.ResourceOwnerPassword => "Password",
            _ => grantType.ToString()
        };
    }

    private ExtractionSource PromptExtractionSource(ExtractionSource current)
    {
        string? source = interactiveConsole.Select(
            "Extraction source",
            Enum.GetNames<ExtractionSource>(),
            choice => choice == current.ToString()
                ? $"[accent]{choice}[/] [secondary](current)[/]"
                : choice);

        return source is null ? current : Enum.Parse<ExtractionSource>(source);
    }

    private void EditDictionary(IDictionary<string, string> values, string nameLabel, string valueLabel)
    {
        while (true)
        {
            string? action = interactiveConsole.Select(
                $"{nameLabel}s",
                ["Back", "Add", "Edit", "Delete"],
                choice => choice switch
                {
                    "Edit" => $"Edit ({values.Count})",
                    "Delete" => $"Delete ({values.Count})",
                    _ => choice
                });

            if (action is null or "Back")
            {
                return;
            }

            switch (action)
            {
                case "Add":
                {
                    string? name = interactiveConsole.TextInput(nameLabel, allowEmpty: false);
                    if (name is null)
                    {
                        break;
                    }

                    string? value = interactiveConsole.TextInput(valueLabel, allowEmpty: true);
                    if (value is not null)
                    {
                        values[name] = value;
                    }

                    break;
                }
                case "Edit":
                {
                    if (!TrySelectDictionaryKey(values, out string? key))
                    {
                        break;
                    }

                    string? value = interactiveConsole.TextInput(valueLabel, values[key!], allowEmpty: true);
                    if (value is not null)
                    {
                        values[key!] = value;
                    }

                    break;
                }
                case "Delete":
                {
                    if (!TrySelectDictionaryKey(values, out string? key))
                    {
                        break;
                    }

                    values.Remove(key!);
                    break;
                }
            }
        }
    }

    private bool TrySelectDictionaryKey(IDictionary<string, string> values, out string? key)
    {
        key = null;
        if (values.Count == 0)
        {
            interactiveConsole.ShowMessage("Entries", "No entries available.");
            return false;
        }

        key = interactiveConsole.Select("Select entry", values.Keys.ToList());
        return key is not null;
    }

    private void FetchAuthValue(AuthEditorState state)
    {
        if (state.Config is OAuth2Config oauth2)
        {
            if (!executor.TryExecute(
                    () => authService.FetchTokenAsync(oauth2).GetAwaiter().GetResult(),
                    message => interactiveConsole.ShowMessage("OAuth 2.0", $"Failed to fetch token: {message}"),
                    out OAuth2Token? token))
            {
                return;
            }

            oauth2.Token = token;
            string expires = token?.ExpiresAt.HasValue == true
                ? token.ExpiresAt.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
                : "N/A";
            interactiveConsole.ShowMessage("OAuth 2.0", $"Token fetched successfully.\nExpires: {expires}");
        }
        else if (state.Config is CustomAuthConfig custom)
        {
            if (!executor.TryExecute(
                    () => authService.ExecuteCustomAuthAsync(custom).GetAwaiter().GetResult(),
                    message => interactiveConsole.ShowMessage("Custom auth", $"Failed to fetch value: {message}"),
                    out string? value))
            {
                return;
            }

            custom.CachedValue = value;
            interactiveConsole.ShowMessage("Custom auth", "Value fetched and cached successfully.");
        }
        else
        {
            interactiveConsole.ShowMessage("Fetch auth", "Selected auth type does not support fetching values.");
        }
    }

    private string? PromptUrl(string? current)
    {
        return interactiveConsole.TextInput(
            "URL",
            current,
            validate: value => IsValidAbsoluteUrl(NormalizeUrlInput(value))
                ? null
                : "Enter a valid absolute URL.");
    }

    private string? PromptMethod(string? current)
    {
        return interactiveConsole.Select(
            "HTTP method",
            ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE", "CONNECT"],
            choice => choice == current ? $"{choice} [accent](current)[/]" : choice);
    }

    private static bool IsValidAbsoluteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    private static string NormalizeUrlInput(string value)
    {
        string trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        return trimmed.Contains("://", StringComparison.Ordinal)
            ? trimmed
            : $"https://{trimmed}";
    }

    private sealed class AuthEditorState
    {
        public string Name { get; set; } = string.Empty;
        public StraumrAuthConfig? Config { get; set; }
        public bool AutoRenew { get; set; } = true;

        public static AuthEditorState CreateNew() => new();

        public static AuthEditorState FromAuth(StraumrAuth auth)
        {
            return new AuthEditorState
            {
                Name = auth.Name,
                AutoRenew = auth.AutoRenewAuth,
                Config = CloneConfig(auth.Config)
            };
        }

        public StraumrAuthConfig CloneConfig()
        {
            return CloneConfig(Config) ?? throw new InvalidOperationException("Auth config must be set.");
        }

        private static StraumrAuthConfig? CloneConfig(StraumrAuthConfig? config)
        {
            return config switch
            {
                null => null,
                BearerAuthConfig bearer => new BearerAuthConfig
                {
                    Prefix = bearer.Prefix,
                    Token = bearer.Token
                },
                BasicAuthConfig basic => new BasicAuthConfig
                {
                    Username = basic.Username,
                    Password = basic.Password
                },
                OAuth2Config oauth2 => new OAuth2Config
                {
                    GrantType = oauth2.GrantType,
                    TokenUrl = oauth2.TokenUrl,
                    ClientId = oauth2.ClientId,
                    ClientSecret = oauth2.ClientSecret,
                    Scope = oauth2.Scope,
                    AuthorizationUrl = oauth2.AuthorizationUrl,
                    RedirectUri = oauth2.RedirectUri,
                    UsePkce = oauth2.UsePkce,
                    CodeChallengeMethod = oauth2.CodeChallengeMethod,
                    Username = oauth2.Username,
                    Password = oauth2.Password,
                    Token = oauth2.Token is null
                        ? null
                        : new OAuth2Token
                        {
                            AccessToken = oauth2.Token.AccessToken,
                            ExpiresAt = oauth2.Token.ExpiresAt,
                            RefreshToken = oauth2.Token.RefreshToken,
                            TokenType = oauth2.Token.TokenType
                        }
                },
                CustomAuthConfig custom => new CustomAuthConfig
                {
                    Url = custom.Url,
                    Method = custom.Method,
                    BodyType = custom.BodyType,
                    Bodies = new Dictionary<BodyType, string>(custom.Bodies),
                    Headers = new Dictionary<string, string>(custom.Headers, StringComparer.OrdinalIgnoreCase),
                    Params = new Dictionary<string, string>(custom.Params, StringComparer.Ordinal),
                    Source = custom.Source,
                    ExtractionExpression = custom.ExtractionExpression,
                    ApplyHeaderName = custom.ApplyHeaderName,
                    ApplyHeaderTemplate = custom.ApplyHeaderTemplate,
                    CachedValue = custom.CachedValue
                },
                _ => null
            };
        }
    }
}
