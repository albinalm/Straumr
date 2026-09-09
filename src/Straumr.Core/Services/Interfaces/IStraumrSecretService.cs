using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrSecretService
{
    Task<IReadOnlyList<StraumrSecret>> ListAsync(CancellationToken cancellationToken = default);

    Task<StraumrSecret> GetAsync(
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);
    Task<StraumrSecret> GetAsync(
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);
    Task<StraumrSecret> CreateAsync(
        StraumrSecret secret,
        CancellationToken cancellationToken = default);
    Task<StraumrSecret> SaveAsync(
        StraumrSecret secret,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
