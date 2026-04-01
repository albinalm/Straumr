using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrAuthService
{
    Task<OAuth2Token> FetchTokenAsync(OAuth2Config config);
    Task<OAuth2Token> EnsureTokenAsync(OAuth2Config config);
    Task<string> ExecuteCustomAuthAsync(CustomAuthConfig config);
}