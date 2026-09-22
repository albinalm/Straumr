namespace Straumr.Core.Services.Interfaces;

public interface IStraumrStateService
{
    StraumrState State { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
