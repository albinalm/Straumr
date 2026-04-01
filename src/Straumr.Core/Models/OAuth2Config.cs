using Straumr.Core.Enums;

namespace Straumr.Core.Models;

public class OAuth2Config : StraumrAuthConfig
{
    public OAuth2GrantType GrantType { get; set; } = OAuth2GrantType.ClientCredentials;

    public string TokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;

    public string AuthorizationUrl { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost:8765/callback";
    public bool UsePkce { get; set; }
    public string CodeChallengeMethod { get; set; } = "S256";

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public OAuth2Token? Token { get; set; }

    public override AuthType Type => AuthType.OAuth2;
}