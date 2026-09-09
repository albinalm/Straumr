using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrOptionsService
{
    StraumrOptions Options { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}