using Straumr.Console.Shared.Helpers;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Models;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Formatting;

/// <summary>
/// How an auth reads wherever it is shown. Both screens use it: the Auths screen is about the auth
/// itself, and the Requests screen names the one a request will send with. Two screens describing
/// one resource differently is worse than either wording, so there is one of each here.
/// </summary>
internal static class AuthFormatting
{
    public static string TypeName(StraumrAuthConfig config) =>
        AuthEditingHelpers.AuthTypeDisplayName(config.Type);

    /// <summary>
    /// The second line of a list row: what kind of auth this is, and the one choice within that kind
    /// that changes what it does.
    /// </summary>
    public static string Meta(StraumrAuthConfig config) => config switch
    {
        OAuth2Config oauth => $"{TypeName(config)} · {AuthEditingHelpers.GrantDisplayName(oauth.GrantType)}",
        CustomAuthConfig custom =>
            $"{TypeName(config)} · {AuthEditingHelpers.ExtractionSourceDisplayName(custom.Source)}",
        _ => TypeName(config)
    };

    /// <summary>
    /// Whether the auth holds something it could authenticate with right now. It is what makes a row
    /// read amber rather than inert, which is the rule every populated value in this app follows.
    /// </summary>
    public static bool HasCredential(StraumrAuthConfig config) => config switch
    {
        BearerAuthConfig bearer => bearer.Token.Length > 0,
        BasicAuthConfig basic => basic.Username.Length > 0 || basic.Password.Length > 0,
        OAuth2Config { Token.IsExpired: false } => true,
        CustomAuthConfig { CachedValue: not null } => true,
        _ => false
    };

    /// <summary>The header the auth writes when it is applied.</summary>
    public static string Injects(StraumrAuthConfig config) =>
        config is CustomAuthConfig custom ? custom.ApplyHeaderName : "Authorization";

    /// <summary>
    /// What the auth currently holds, and what that means for the next send.
    /// </summary>
    /// <remarks>
    /// The palette answers it rather than a word of its own: amber is what every populated value in
    /// this app reads as, inert grey is empty, and red is the reason an operation will fail before it
    /// starts. Green is not available — it belongs to the active workspace alone — which is why an
    /// expired token that will renew itself reads amber rather than green-once-renewed.
    /// </remarks>
    public static AuthStatus Status(StraumrAuth auth) => auth.Config switch
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

    private static AuthStatus Held(string text) => new(text, StraumrStyles.AmberText);

    private static AuthStatus Empty(string text) => new(text, StraumrStyles.MutedText);

    private static AuthStatus Failing(string text) => new(text, StraumrStyles.RedText);
}

/// <summary>What an auth holds, said in one phrase and one colour.</summary>
internal readonly record struct AuthStatus(string Text, TextBlockStyle Style);
