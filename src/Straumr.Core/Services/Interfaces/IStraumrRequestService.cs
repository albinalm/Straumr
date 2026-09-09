using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrRequestService
{
    Task<IReadOnlyList<StraumrRequest>> ListAsync(
        StraumrWorkspaceEntry workspace,
        CancellationToken cancellationToken = default);

    Task<StraumrRequest> GetAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);

    Task<StraumrRequest> GetAsync(
        StraumrWorkspaceEntry workspace,
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);

    Task<StraumrRequest> CreateAsync(
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        CancellationToken cancellationToken = default);

    Task<StraumrRequest> SaveAsync(
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(string ResolvedUrl, IReadOnlyList<string> Warnings)> ResolveUrlAsync(
        StraumrRequest request,
        CancellationToken cancellationToken = default);

    Task<StraumrResponse> SendAsync(
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        SendOptions? options = null,
        CancellationToken cancellationToken = default);
}
