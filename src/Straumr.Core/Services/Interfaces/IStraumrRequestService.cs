using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrRequestService
{
    Task<StraumrRequest> Get(Guid id);
    Task Create(StraumrRequest request);
    bool Validate(StraumrRequest request, out string? validationMessage);
}
