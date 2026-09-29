namespace Straumr.Core.Services.Interfaces;

public interface IStraumrRequestCopyService
{
    Task<RequestCopyPlanModel> PrepareAsync(StraumrWorkspaceEntry source, Guid id, StraumrWorkspaceEntry destination,
        string name, CancellationToken cancellationToken = default);
    Task<CopyNameConflictModel?> FindNameConflictAsync(RequestCopyPlanModel plan, CancellationToken cancellationToken = default);
    Task CopyAsync(RequestCopyPlanModel plan, CancellationToken cancellationToken = default);
}
