using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrRequestService
{
    Task<StraumrRequest> GetAsync(string identifier, StraumrWorkspaceEntry? workspace = null);
    Task<StraumrRequest> PeekByIdAsync(Guid id, StraumrWorkspaceEntry? workspace = null);
    Task<(string ResolvedUrl, IReadOnlyList<string> Warnings)> ResolveUrlAsync(StraumrRequest request);
    Task CreateAsync(StraumrRequest request, StraumrWorkspaceEntry? workspace = null);
    Task UpdateAsync(StraumrRequest request, StraumrWorkspaceEntry? workspace = null);
    Task<StraumrResponse> SendAsync(StraumrRequest request, SendOptions? options = null, StraumrWorkspaceEntry? workspace = null);
    Task<StraumrRequest> CopyAsync(string identifier, string newName, StraumrWorkspaceEntry? workspace = null);
    Task DeleteAsync(string identifier, StraumrWorkspaceEntry? workspace = null);
    Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier, StraumrWorkspaceEntry? workspace = null);
    void ApplyEdit(Guid requestId, string tempPath, StraumrWorkspaceEntry? workspace = null);
}
