using System.ComponentModel;
using JetBrains.Annotations;
using Spectre.Console.Cli;

namespace Straumr.Console.Cli.Commands.Auth;

[UsedImplicitly]
public sealed class AuthCreateCommandSettings : CommandSettings
{
    [CommandArgument(0, "[Name]")]
    [Description("Name of the auth to create")]
    public string? Name { get; [UsedImplicitly] set; }

    [CommandOption("-t|--type")]
    [Description("Auth type: bearer, basic, oauth2, oauth2-client-credentials, oauth2-authorization-code, oauth2-password, custom")]
    public string? Type { get; [UsedImplicitly] set; }

    [CommandOption("-s|--secret")]
    [Description("Token or secret value (bearer: token value)")]
    public string? Secret { get; [UsedImplicitly] set; }

    [CommandOption("--prefix")]
    [Description("Token prefix for bearer auth (default: Bearer)")]
    public string? Prefix { get; [UsedImplicitly] set; }

    [CommandOption("-u|--username")]
    [Description("Username for basic auth or OAuth2 password grant")]
    public string? Username { get; [UsedImplicitly] set; }

    [CommandOption("-p|--password")]
    [Description("Password for basic auth or OAuth2 password grant")]
    public string? Password { get; [UsedImplicitly] set; }

    [CommandOption("-g|--grant")]
    [Description("OAuth2 grant type when --type is oauth2: client-credentials, authorization-code, password")]
    public string? GrantType { get; [UsedImplicitly] set; }

    [CommandOption("--token-url")]
    [Description("OAuth2 token endpoint URL")]
    public string? TokenUrl { get; [UsedImplicitly] set; }

    [CommandOption("--client-id")]
    [Description("OAuth2 client ID")]
    public string? ClientId { get; [UsedImplicitly] set; }

    [CommandOption("--client-secret")]
    [Description("OAuth2 client secret")]
    public string? ClientSecret { get; [UsedImplicitly] set; }

    [CommandOption("--scope")]
    [Description("OAuth2 scope")]
    public string? Scope { get; [UsedImplicitly] set; }

    [CommandOption("--authorization-url")]
    [Description("OAuth2 authorization URL (authorization code grant)")]
    public string? AuthorizationUrl { get; [UsedImplicitly] set; }

    [CommandOption("--redirect-uri")]
    [Description("OAuth2 redirect URI (authorization code grant, default: http://localhost:8765/callback)")]
    public string? RedirectUri { get; [UsedImplicitly] set; }

    [CommandOption("--pkce")]
    [Description("PKCE mode for authorization code grant: S256, plain, disabled")]
    public string? Pkce { get; [UsedImplicitly] set; }

    [CommandOption("--custom-url")]
    [Description("Custom auth request URL")]
    public string? CustomUrl { get; [UsedImplicitly] set; }

    [CommandOption("--custom-method")]
    [Description("Custom auth request method (default: POST)")]
    public string? CustomMethod { get; [UsedImplicitly] set; }

    [CommandOption("--custom-header")]
    [Description("Custom auth request header in \"Name: Value\" format (repeatable)")]
    public string[]? CustomHeaders { get; [UsedImplicitly] set; }

    [CommandOption("--custom-param")]
    [Description("Custom auth request param in \"key=value\" format (repeatable)")]
    public string[]? CustomParams { get; [UsedImplicitly] set; }

    [CommandOption("--custom-body")]
    [Description("Custom auth request body content")]
    public string? CustomBody { get; [UsedImplicitly] set; }

    [CommandOption("--custom-body-type")]
    [Description("Custom auth body type: json, xml, text, form, multipart, raw (default: json)")]
    public string? CustomBodyType { get; [UsedImplicitly] set; }

    [CommandOption("--extraction-source")]
    [Description("Custom auth extraction source: jsonpath, header, regex")]
    public string? ExtractionSource { get; [UsedImplicitly] set; }

    [CommandOption("--extraction-expression")]
    [Description("Custom auth extraction expression (e.g. access_token, X-Auth-Token, or regex)")]
    public string? ExtractionExpression { get; [UsedImplicitly] set; }

    [CommandOption("--apply-header-name")]
    [Description("Custom auth header name to apply (default: Authorization)")]
    public string? ApplyHeaderName { get; [UsedImplicitly] set; }

    [CommandOption("--apply-header-template")]
    [Description("Custom auth header value template with {{value}} placeholder (default: Bearer {{value}})")]
    public string? ApplyHeaderTemplate { get; [UsedImplicitly] set; }

    [CommandOption("--no-auto-renew")]
    [Description("Disable auto-renewal of auth tokens")]
    public bool NoAutoRenew { get; [UsedImplicitly] set; }

    public bool AutoRenew => !NoAutoRenew;

    [CommandOption("-j|--json")]
    [Description("Output the created auth as JSON")]
    public bool Json { get; [UsedImplicitly] set; }

    [CommandOption("-w|--workspace")]
    [Description("Target workspace name or ID (overrides the current workspace for this command)")]
    public string? Workspace { get; [UsedImplicitly] set; }
}
