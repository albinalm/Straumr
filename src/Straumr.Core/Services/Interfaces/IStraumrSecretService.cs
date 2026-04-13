using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrSecretService
{
    Task<StraumrSecret> GetAsync(string identifier);
    Task<StraumrSecret> PeekByIdAsync(Guid id);
    Task CreateAsync(StraumrSecret secret);
    Task UpdateAsync(StraumrSecret secret);
    Task<StraumrSecret> CopyAsync(string identifier, string newName);
    Task DeleteAsync(string identifier);
    Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier);
    void ApplyEdit(Guid secretId, string tempPath);
}
