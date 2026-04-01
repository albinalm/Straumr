namespace Straumr.Core.Models;

public class BearerAuthConfig
{
    public string Token { get; set; } = string.Empty;
    public string Prefix { get; set; } = "Bearer";
}
