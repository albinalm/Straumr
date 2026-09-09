using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrWorkspaceService
{
    Task<IReadOnlyList<StraumrWorkspace>> ListAsync(CancellationToken cancellationToken = default);

    Task<StraumrWorkspace> GetAsync(
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);
    Task<StraumrWorkspace> GetAsync(
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default);

    Task<StraumrWorkspace> CreateAsync(
        StraumrWorkspace workspace,
        string? outputDir = null,
        CancellationToken cancellationToken = default);

    Task<StraumrWorkspace> SaveAsync(
        StraumrWorkspace workspace,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StraumrWorkspaceEntry> CopyAsync(
        Guid id,
        string newName,
        string? outputDir = null,
        CancellationToken cancellationToken = default);

    Task<StraumrWorkspaceEntry> ImportAsync(string path, CancellationToken cancellationToken = default);

    Task<string> ExportAsync(Guid id, string outputDir, CancellationToken cancellationToken = default);

    StraumrWorkspaceEntry GetEntry(Guid id);
}
