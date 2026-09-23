namespace Straumr.Core.Services.Interfaces;

public interface IStraumrAuthService
{
    string PathFor(StraumrWorkspaceEntry workspace, Guid id);

    Task<IReadOnlyList<StraumrAuth>> ListAsync(
        StraumrWorkspaceEntry workspace,
        CancellationToken cancellationToken = default);

    Task<StraumrAuth> GetAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);

    Task<StraumrAuth> GetAsync(
        StraumrWorkspaceEntry workspace,
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);

    Task<StraumrAuth> CreateAsync(
        StraumrWorkspaceEntry workspace,
        StraumrAuth auth,
        CancellationToken cancellationToken = default);

    Task<StraumrAuth> SaveAsync(
        StraumrWorkspaceEntry workspace,
        StraumrAuth auth,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<OAuth2Token> FetchTokenAsync(OAuth2Config config, CancellationToken cancellationToken = default);

    Task<OAuth2Token> EnsureTokenAsync(OAuth2Config config, CancellationToken cancellationToken = default);

    Task<OAuth2Token> RenewTokenAsync(OAuth2Config config, CancellationToken cancellationToken = default);

    Task<string> ExecuteCustomAuthAsync(CustomAuthConfig config, CancellationToken cancellationToken = default);
}
