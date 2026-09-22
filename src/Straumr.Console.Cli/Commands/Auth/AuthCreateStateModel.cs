using Straumr.Core.Models;

namespace Straumr.Console.Cli.Commands.Auth;

internal sealed class AuthCreateStateModel(string name)
{
    public string Name { get; set; } = name;
    public StraumrAuthConfig? Auth { get; set; }
    public bool AutoRenewAuth { get; set; } = true;

    public StraumrAuth ToAuth() =>
        new()
        {
            Name = Name,
            Config = Auth ?? throw new InvalidOperationException("Auth must be configured before saving"),
            AutoRenewAuth = AutoRenewAuth
        };
}
