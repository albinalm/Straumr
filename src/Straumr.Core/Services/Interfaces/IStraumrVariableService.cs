namespace Straumr.Core.Services.Interfaces;

public interface IStraumrVariableService
{
    string PathFor(StraumrWorkspaceEntry workspace, Guid id);

    Task<IReadOnlyList<StraumrVariable>> ListAsync(
        StraumrWorkspaceEntry workspace,
        CancellationToken cancellationToken = default);

    Task<StraumrVariable> GetAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);

    Task<StraumrVariable> GetAsync(
        StraumrWorkspaceEntry workspace,
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);

    Task<StraumrVariable> CreateAsync(
        StraumrWorkspaceEntry workspace,
        StraumrVariable variable,
        CancellationToken cancellationToken = default);

    Task<StraumrVariable> SaveAsync(
        StraumrWorkspaceEntry workspace,
        StraumrVariable variable,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        CancellationToken cancellationToken = default);
}
