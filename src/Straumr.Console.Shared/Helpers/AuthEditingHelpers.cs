using Straumr.Core.Enums;
using Straumr.Core.Models;

namespace Straumr.Console.Shared.Helpers;

/// <summary>
/// What an auth is called, offered and understood as, shared by the CLI's prompt flow and the TUI's
/// form so the two cannot drift. It is the auth counterpart of <see cref="RequestEditingHelpers"/>
/// and holds only naming and construction: nothing here reads a file or sends a request.
/// </summary>
public static class AuthEditingHelpers
{
    /// <summary>The auth types a resource can be given, in the order they are offered.</summary>
    /// <remarks>
    /// <see cref="AuthType.None"/> is absent: it is what a request means by having no auth at all,
    /// not something an auth resource can be.
    /// </remarks>
    public static readonly AuthType[] AuthTypes =
        [AuthType.Bearer, AuthType.Basic, AuthType.OAuth2, AuthType.Custom];

    public static readonly OAuth2GrantType[] OAuth2Grants =
        [OAuth2GrantType.ClientCredentials, OAuth2GrantType.AuthorizationCode,
         OAuth2GrantType.ResourceOwnerPassword];

    public static readonly ExtractionSource[] ExtractionSources =
        [ExtractionSource.JsonPath, ExtractionSource.ResponseHeader, ExtractionSource.Regex];

    /// <summary>The PKCE code challenge methods an authorization code grant can ask for.</summary>
    public static readonly string[] CodeChallengeMethods = ["S256", "plain"];

    public static string AuthTypeDisplayName(AuthType type)
    {
        return type switch
        {
            AuthType.Bearer => "Bearer",
            AuthType.Basic => "Basic",
            AuthType.OAuth2 => "OAuth 2.0",
            AuthType.Custom => "Custom",
            _ => "None"
        };
    }

    public static string GrantDisplayName(OAuth2GrantType grant)
    {
        return grant switch
        {
            OAuth2GrantType.ClientCredentials => "Client Credentials",
            OAuth2GrantType.AuthorizationCode => "Authorization Code",
            OAuth2GrantType.ResourceOwnerPassword => "Resource Owner Password",
            _ => grant.ToString()
        };
    }

    public static string ExtractionSourceDisplayName(ExtractionSource source)
    {
        return source switch
        {
            ExtractionSource.JsonPath => "JSON path",
            ExtractionSource.ResponseHeader => "Response header",
            ExtractionSource.Regex => "Regex",
            _ => source.ToString()
        };
    }

    /// <summary>
    /// What an extraction expression looks like for this source, for a prompt or a placeholder. The
    /// expression means something different in each of the three, and nothing else on screen says
    /// which of the three is being written.
    /// </summary>
    public static string ExtractionExpressionHint(ExtractionSource source)
    {
        return source switch
        {
            ExtractionSource.JsonPath => "JSON path (e.g. access_token or data.token)",
            ExtractionSource.ResponseHeader => "Header name (e.g. X-Auth-Token)",
            ExtractionSource.Regex => "Regex with capture group (e.g. token\":\"([^\"]+)\")",
            _ => "Expression"
        };
    }

    /// <summary>
    /// Whether this auth holds a value that can be fetched ahead of a send. Bearer and Basic are the
    /// credential they carry; OAuth2 and Custom go and get one.
    /// </summary>
    public static bool SupportsFetch(StraumrAuthConfig? config) => config is OAuth2Config or CustomAuthConfig;

    /// <summary>A new configuration of the given type, with the defaults its own model declares.</summary>
    public static StraumrAuthConfig CreateConfig(AuthType type)
    {
        return type switch
        {
            AuthType.Bearer => new BearerAuthConfig(),
            AuthType.Basic => new BasicAuthConfig(),
            AuthType.OAuth2 => new OAuth2Config(),
            AuthType.Custom => new CustomAuthConfig(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "An auth resource must have a type.")
        };
    }
}
