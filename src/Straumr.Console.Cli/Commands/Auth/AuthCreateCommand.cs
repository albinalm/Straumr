using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Straumr.Console.Cli.Infrastructure;
using Straumr.Console.Cli.Models;
using Straumr.Console.Shared.Console;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using static Straumr.Console.Cli.Helpers.AuthCommandHelpers;
using static Straumr.Console.Cli.Helpers.ConsoleHelpers;
using static Straumr.Console.Cli.Commands.Request.RequestCommandHelpers;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Straumr.Console.Cli.Commands.Auth;

public class AuthCreateCommand(
    IStraumrOptionsService optionsService,
    IStraumrWorkspaceService workspaceService,
    IStraumrAuthService authService,
    IInteractiveConsole interactiveConsole)
    : AsyncCommand<AuthCreateCommand.Settings>
{
    private const string ActionFinish = "Finish";
    private const string ActionName = "Edit name";
    private const string ActionConfigure = "Configure auth";
    private const string ActionFetch = "Fetch token/value";
    private const string ActionAutoRenew = "Auto-renew auth";

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

        if (settings.Type is not null)
        {
            return await ExecuteInlineAsync(settings);
        }

        var state = new CreateAuthState(settings.Name ?? string.Empty);

        while (true)
        {
            string? action = await PromptCreateMenuAsync(state);
            if (action is null)
            {
                return 1;
            }

            if (action == ActionFinish)
            {
                if (await TryCreateAuthAsync(state))
                {
                    return 0;
                }

                continue;
            }

            await HandleCreateActionAsync(state, action);
        }
    }

    private async Task<int> ExecuteInlineAsync(Settings settings)
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
            await authService.CreateAsync(auth);

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

    private StraumrAuthConfig? BuildInlineConfig(Settings settings)
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

    private static OAuth2GrantType ResolveOAuth2GrantType(Settings settings)
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

    private async Task<string?> PromptCreateMenuAsync(CreateAuthState state)
    {
        string nameDisplay = string.IsNullOrWhiteSpace(state.Name)
            ? "[grey]not set[/]"
            : $"[blue]{Markup.Escape(state.Name)}[/]";
        string authDisplay = AuthDisplayName(state.Auth);
        string autoRenewDisplay = state.AutoRenewAuth ? "[green]enabled[/]" : "[grey]disabled[/]";
        var menuChoices = new List<string> { ActionFinish, ActionName, ActionConfigure, ActionAutoRenew };

        if (SupportsAuthFetch(state.Auth))
        {
            menuChoices.Add(ActionFetch);
        }

        return await interactiveConsole.SelectAsync("Auth setup", menuChoices,
            choice => choice switch
            {
                ActionName => $"Name: {nameDisplay}",
                ActionConfigure => $"Auth: {authDisplay}",
                ActionAutoRenew => $"Auto-renew auth: {autoRenewDisplay}",
                _ => choice
            });
    }

    private async Task<bool> TryCreateAuthAsync(CreateAuthState state)
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
            await authService.CreateAsync(auth);
            AnsiConsole.MarkupLine($"[green]Created auth[/] [bold]{auth.Name}[/] ({auth.Id})");
            return true;
        }
        catch (Exception ex)
        {
            interactiveConsole.ShowMessage($"[red]{Markup.Escape(ex.Message)}[/]");
            return false;
        }
    }

    private async Task HandleCreateActionAsync(CreateAuthState state, string action)
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
            case ActionConfigure:
                state.Auth = await EditAuthAsync(interactiveConsole, state.Auth);
                break;
            case ActionAutoRenew:
                state.AutoRenewAuth = !state.AutoRenewAuth;
                break;
            case ActionFetch:
                await FetchAuthValueAsync(interactiveConsole, authService, state.Auth);
                break;
        }
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[Name]")]
        [Description("Name of the auth to create")]
        public string? Name { get; set; }

        [CommandOption("-t|--type")]
        [Description("Auth type: bearer, basic, oauth2, oauth2-client-credentials, oauth2-authorization-code, oauth2-password, custom")]
        public string? Type { get; set; }

        [CommandOption("-s|--secret")]
        [Description("Token or secret value (bearer: token value)")]
        public string? Secret { get; set; }

        [CommandOption("--prefix")]
        [Description("Token prefix for bearer auth (default: Bearer)")]
        public string? Prefix { get; set; }

        [CommandOption("-u|--username")]
        [Description("Username for basic auth or OAuth2 password grant")]
        public string? Username { get; set; }

        [CommandOption("-p|--password")]
        [Description("Password for basic auth or OAuth2 password grant")]
        public string? Password { get; set; }

        [CommandOption("-g|--grant")]
        [Description("OAuth2 grant type when --type is oauth2: client-credentials, authorization-code, password")]
        public string? GrantType { get; set; }

        [CommandOption("--token-url")]
        [Description("OAuth2 token endpoint URL")]
        public string? TokenUrl { get; set; }

        [CommandOption("--client-id")]
        [Description("OAuth2 client ID")]
        public string? ClientId { get; set; }

        [CommandOption("--client-secret")]
        [Description("OAuth2 client secret")]
        public string? ClientSecret { get; set; }

        [CommandOption("--scope")]
        [Description("OAuth2 scope")]
        public string? Scope { get; set; }

        [CommandOption("--authorization-url")]
        [Description("OAuth2 authorization URL (authorization code grant)")]
        public string? AuthorizationUrl { get; set; }

        [CommandOption("--redirect-uri")]
        [Description("OAuth2 redirect URI (authorization code grant, default: http://localhost:8765/callback)")]
        public string? RedirectUri { get; set; }

        [CommandOption("--pkce")]
        [Description("PKCE mode for authorization code grant: S256, plain, disabled")]
        public string? Pkce { get; set; }

        [CommandOption("--custom-url")]
        [Description("Custom auth request URL")]
        public string? CustomUrl { get; set; }

        [CommandOption("--custom-method")]
        [Description("Custom auth request method (default: POST)")]
        public string? CustomMethod { get; set; }

        [CommandOption("--custom-header")]
        [Description("Custom auth request header in \"Name: Value\" format (repeatable)")]
        public string[]? CustomHeaders { get; set; }

        [CommandOption("--custom-param")]
        [Description("Custom auth request param in \"key=value\" format (repeatable)")]
        public string[]? CustomParams { get; set; }

        [CommandOption("--custom-body")]
        [Description("Custom auth request body content")]
        public string? CustomBody { get; set; }

        [CommandOption("--custom-body-type")]
        [Description("Custom auth body type: json, xml, text, form, multipart, raw (default: json)")]
        public string? CustomBodyType { get; set; }

        [CommandOption("--extraction-source")]
        [Description("Custom auth extraction source: jsonpath, header, regex")]
        public string? ExtractionSource { get; set; }

        [CommandOption("--extraction-expression")]
        [Description("Custom auth extraction expression (e.g. access_token, X-Auth-Token, or regex)")]
        public string? ExtractionExpression { get; set; }

        [CommandOption("--apply-header-name")]
        [Description("Custom auth header name to apply (default: Authorization)")]
        public string? ApplyHeaderName { get; set; }

        [CommandOption("--apply-header-template")]
        [Description("Custom auth header value template with {{value}} placeholder (default: Bearer {{value}})")]
        public string? ApplyHeaderTemplate { get; set; }

        [CommandOption("--no-auto-renew")]
        [Description("Disable auto-renewal of auth tokens")]
        public bool NoAutoRenew { get; set; }

        public bool AutoRenew => !NoAutoRenew;

        [CommandOption("-j|--json")]
        [Description("Output the created auth as JSON")]
        public bool Json { get; set; }

        [CommandOption("-w|--workspace")]
        [Description("Target workspace name or ID (overrides the current workspace for this command)")]
        public string? Workspace { get; set; }
    }

    private sealed class CreateAuthState(string name)
    {
        public string Name { get; set; } = name;
        public StraumrAuthConfig? Auth { get; set; }
        public bool AutoRenewAuth { get; set; } = true;

        public StraumrAuth ToAuth()
        {
            return new StraumrAuth
            {
                Name = Name,
                Config = Auth ?? throw new InvalidOperationException("Auth must be configured before saving"),
                AutoRenewAuth = AutoRenewAuth
            };
        }
    }
}
