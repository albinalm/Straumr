using Straumr.Core.Enums;
using Straumr.Core.Models;

namespace Straumr.Console.Shared.Helpers;

public static class AuthEditingHelpers
{
    public static readonly AuthType[] AuthTypes =
        [AuthType.Bearer, AuthType.Basic, AuthType.OAuth2, AuthType.Custom];

    public static readonly OAuth2GrantType[] OAuth2Grants =
    [
        OAuth2GrantType.ClientCredentials, OAuth2GrantType.AuthorizationCode,
        OAuth2GrantType.ResourceOwnerPassword
    ];

    public static readonly ExtractionSource[] ExtractionSources =
        [ExtractionSource.JsonPath, ExtractionSource.ResponseHeader, ExtractionSource.Regex];

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

    public static bool SupportsFetch(StraumrAuthConfig? config) => config is OAuth2Config or CustomAuthConfig;

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
