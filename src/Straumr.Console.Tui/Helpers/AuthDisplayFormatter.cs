using Straumr.Core.Models;

namespace Straumr.Console.Tui.Helpers;

public static class AuthDisplayFormatter
{
    public static string GetAuthTypeName(StraumrAuthConfig? config)
    {
        return config switch
        {
            null => "None",
            BearerAuthConfig => "Bearer",
            BasicAuthConfig => "Basic",
            OAuth2Config => "OAuth 2.0",
            CustomAuthConfig => "Custom",
            _ => "Unknown"
        };
    }

    public static string DescribeAuth(StraumrAuthConfig? config)
    {
        return config switch
        {
            null => "[secondary]none[/]",
            BearerAuthConfig bearer => string.IsNullOrWhiteSpace(bearer.Token)
                ? "[blue]Bearer[/] [secondary]token not set[/]"
                : "[blue]Bearer[/] [success]token set[/]",
            BasicAuthConfig basic => string.IsNullOrWhiteSpace(basic.Username)
                ? "[blue]Basic[/] [secondary]username not set[/]"
                : $"[blue]Basic[/] [green]{basic.Username}[/]",
            OAuth2Config => "[blue]OAuth 2.0[/]",
            CustomAuthConfig => "[blue]Custom[/]",
            _ => "[secondary]none[/]"
        };
    }

    public static bool SupportsAuthFetch(StraumrAuthConfig? config)
        => config is OAuth2Config or CustomAuthConfig;
}
