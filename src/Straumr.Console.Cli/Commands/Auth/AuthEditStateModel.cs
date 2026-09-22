using Straumr.Core.Models;

namespace Straumr.Console.Cli.Commands.Auth;

internal sealed class AuthEditStateModel
{
    private AuthEditStateModel(string name, StraumrAuthConfig? auth, bool autoRenewAuth)
    {
        Name = name;
        Auth = auth;
        AutoRenewAuth = autoRenewAuth;
    }

    public string Name { get; set; }
    public StraumrAuthConfig? Auth { get; set; }
    public bool AutoRenewAuth { get; set; }

    public static AuthEditStateModel FromAuth(StraumrAuth auth) => new(auth.Name, auth.Config, auth.AutoRenewAuth);

    public void ApplyTo(StraumrAuth auth)
    {
        auth.Name = Name;
        auth.Config = Auth ?? throw new InvalidOperationException("Auth config must be set.");
        auth.AutoRenewAuth = AutoRenewAuth;
    }
}
