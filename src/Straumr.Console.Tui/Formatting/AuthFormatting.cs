using Straumr.Core.Models;

namespace Straumr.Console.Tui.Formatting;

internal static class AuthFormatting
{
    public static string TypeName(StraumrAuthConfig config) =>
        AuthEditingHelpers.AuthTypeDisplayName(config.Type);

    public static string Meta(StraumrAuthConfig config) => config switch
    {
        OAuth2Config oauth => $"{TypeName(config)} · {AuthEditingHelpers.GrantDisplayName(oauth.GrantType)}",
        CustomAuthConfig custom =>
            $"{TypeName(config)} · {AuthEditingHelpers.ExtractionSourceDisplayName(custom.Source)}",
        _ => TypeName(config)
    };

    public static bool HasCredential(StraumrAuthConfig config) => config switch
    {
        BearerAuthConfig bearer => bearer.Token.Length > 0,
        BasicAuthConfig basic => basic.Username.Length > 0 || basic.Password.Length > 0,
        OAuth2Config { Token.IsExpired: false } => true,
        CustomAuthConfig { CachedValue: not null } => true,
        _ => false
    };

    public static string Injects(StraumrAuthConfig config) =>
        config is CustomAuthConfig custom ? custom.ApplyHeaderName : "Authorization";

    public static AuthStatusModel Status(StraumrAuth auth) => auth.Config switch
    {
        BearerAuthConfig { Token.Length: > 0 } => Held("token set"),
        BearerAuthConfig => Empty("no token"),
        BasicAuthConfig basic when basic.Username.Length > 0 || basic.Password.Length > 0 =>
            Held("credentials set"),
        BasicAuthConfig => Empty("no credentials"),
        OAuth2Config { Token: null } => auth.AutoRenewAuth
            ? Empty("no token; fetched on send")
            : Failing("no token; auto-renew off"),
        OAuth2Config { Token.IsExpired: true } => auth.AutoRenewAuth
            ? Held("expired; renews on send")
            : Failing("expired; auto-renew off"),
        OAuth2Config => Held("valid"),
        CustomAuthConfig { CachedValue: null } => auth.AutoRenewAuth
            ? Empty("no value; fetched on send")
            : Failing("no value; auto-renew off"),
        CustomAuthConfig => Held("value cached"),
        _ => Empty("not configured")
    };

    private static AuthStatusModel Held(string text) => new(text, StraumrStyleService.AmberText);

    private static AuthStatusModel Empty(string text) => new(text, StraumrStyleService.MutedText);

    private static AuthStatusModel Failing(string text) => new(text, StraumrStyleService.RedText);
}
