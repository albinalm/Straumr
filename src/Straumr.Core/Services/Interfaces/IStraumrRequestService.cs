using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrRequestService
{
    Task<StraumrRequest> Get(string id);
    Task Create(StraumrRequest request);
}