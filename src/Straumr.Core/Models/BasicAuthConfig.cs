using Straumr.Core.Enums;

namespace Straumr.Core.Models;

public class BasicAuthConfig : StraumrAuthConfig
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public override AuthType Type => AuthType.Basic;
}