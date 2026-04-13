using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrAuthService
{
    Task<IReadOnlyList<StraumrAuth>> ListAsync(StraumrWorkspaceEntry? workspace = null);
    Task<StraumrAuth> GetAsync(string identifier, StraumrWorkspaceEntry? workspace = null);
    Task<StraumrAuth> PeekByIdAsync(Guid id, StraumrWorkspaceEntry? workspace = null);
    Task StampAccessAsync(Guid id, StraumrWorkspaceEntry? workspace = null);
    Task CreateAsync(StraumrAuth auth, StraumrWorkspaceEntry? workspace = null);
    Task UpdateAsync(StraumrAuth auth, StraumrWorkspaceEntry? workspace = null);
    Task<StraumrAuth> CopyAsync(string identifier, string newName, StraumrWorkspaceEntry? workspace = null);
    Task DeleteAsync(string identifier, StraumrWorkspaceEntry? workspace = null);
    Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier, StraumrWorkspaceEntry? workspace = null);
    void ApplyEdit(Guid authId, string tempPath, StraumrWorkspaceEntry? workspace = null);

    Task<OAuth2Token> FetchTokenAsync(OAuth2Config config);
    Task<OAuth2Token> EnsureTokenAsync(OAuth2Config config);
    Task<string> ExecuteCustomAuthAsync(CustomAuthConfig config);
}
