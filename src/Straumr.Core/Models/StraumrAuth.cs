namespace Straumr.Core.Models;

public class StraumrAuth : StraumrModelBase
{
    public required StraumrAuthConfig Config { get; set; }
    public bool AutoRenewAuth { get; set; } = true;

    public StraumrAuth CopyAs(string name) => new()
    {
        Name = name,
        Config = Config switch
        {
            BearerAuthConfig bearer => new BearerAuthConfig
            {
                Token = bearer.Token,
                Prefix = bearer.Prefix
            },
            BasicAuthConfig basic => new BasicAuthConfig
            {
                Username = basic.Username,
                Password = basic.Password
            },
            OAuth2Config oauth => new OAuth2Config
            {
                GrantType = oauth.GrantType,
                TokenUrl = oauth.TokenUrl,
                ClientId = oauth.ClientId,
                ClientSecret = oauth.ClientSecret,
                Scope = oauth.Scope,
                AuthorizationUrl = oauth.AuthorizationUrl,
                RedirectUri = oauth.RedirectUri,
                UsePkce = oauth.UsePkce,
                CodeChallengeMethod = oauth.CodeChallengeMethod,
                Username = oauth.Username,
                Password = oauth.Password,
                Token = oauth.Token is null
                    ? null
                    : new OAuth2Token
                    {
                        AccessToken = oauth.Token.AccessToken,
                        RefreshToken = oauth.Token.RefreshToken,
                        TokenType = oauth.Token.TokenType,
                        ExpiresAt = oauth.Token.ExpiresAt
                    }
            },
            CustomAuthConfig custom => new CustomAuthConfig
            {
                Url = custom.Url,
                Method = custom.Method,
                BodyType = custom.BodyType,
                Bodies = new Dictionary<Enums.BodyType, string>(custom.Bodies),
                Headers = new Dictionary<string, string>(custom.Headers, custom.Headers.Comparer),
                Params = new Dictionary<string, string>(custom.Params, custom.Params.Comparer),
                Source = custom.Source,
                ExtractionExpression = custom.ExtractionExpression,
                ApplyHeaderName = custom.ApplyHeaderName,
                ApplyHeaderTemplate = custom.ApplyHeaderTemplate,
                CachedValue = custom.CachedValue
            },
            _ => throw new InvalidOperationException($"Unsupported auth configuration: {Config.GetType().Name}")
        },
        AutoRenewAuth = AutoRenewAuth
    };
}
