using Straumr.Core.Enums;

namespace Straumr.Core.Models;

public class BearerAuthConfig : StraumrAuthConfig
{
    public string Token { get; set; } = string.Empty;
    public string Prefix { get; set; } = "Bearer";

    public override AuthType Type => AuthType.Bearer;
}