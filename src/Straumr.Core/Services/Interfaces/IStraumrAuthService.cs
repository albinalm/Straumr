using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrAuthService
{
    Task<IReadOnlyList<StraumrAuth>> ListAsync();
    Task<StraumrAuth> GetAsync(string identifier);
    Task<StraumrAuth> PeekByIdAsync(Guid id);
    Task CreateAsync(StraumrAuth auth);
    Task UpdateAsync(StraumrAuth auth);
    Task DeleteAsync(string identifier);
    Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier);
    void ApplyEdit(Guid authId, string tempPath);

    Task<OAuth2Token> FetchTokenAsync(OAuth2Config config);
    Task<OAuth2Token> EnsureTokenAsync(OAuth2Config config);
    Task<string> ExecuteCustomAuthAsync(CustomAuthConfig config);
}
