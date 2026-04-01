using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrAuthTemplateService
{
    Task<IReadOnlyList<StraumrAuthTemplate>> ListAsync();
    Task<StraumrAuthTemplate> GetAsync(string identifier);
    Task<StraumrAuthTemplate> PeekByIdAsync(Guid id);
    Task CreateAsync(StraumrAuthTemplate template);
    Task UpdateAsync(StraumrAuthTemplate template);
    Task DeleteAsync(string identifier);
}