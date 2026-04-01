namespace Straumr.Core.Models;

public class OAuth2Token
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow >= ExpiresAt.Value;
}