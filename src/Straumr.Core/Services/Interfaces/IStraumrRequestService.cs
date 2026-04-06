using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrRequestService
{
    Task<StraumrRequest> GetAsync(string identifier);
    Task<StraumrRequest> PeekByIdAsync(Guid id);
    Task<(string ResolvedUrl, IReadOnlyList<string> Warnings)> ResolveUrlAsync(StraumrRequest request);
    Task CreateAsync(StraumrRequest request);
    Task UpdateAsync(StraumrRequest request);
    Task<StraumrResponse> SendAsync(StraumrRequest request, SendOptions? options = null);
    Task<StraumrRequest> CopyAsync(string identifier, string newName);
    Task DeleteAsync(string identifier);
    Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier);
    void ApplyEdit(Guid requestId, string tempPath);
}
