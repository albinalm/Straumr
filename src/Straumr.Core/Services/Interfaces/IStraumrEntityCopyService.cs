namespace Straumr.Core.Services.Interfaces;

public interface IStraumrEntityCopyService
{
    Task<EntityCopyPlanModel> PrepareRequestAsync(StraumrWorkspaceEntry source, Guid id, StraumrWorkspaceEntry destination,
        string name, CancellationToken cancellationToken = default);
    Task<EntityCopyPlanModel> PrepareAuthAsync(StraumrWorkspaceEntry source, Guid id, StraumrWorkspaceEntry destination,
        string name, CancellationToken cancellationToken = default);
    Task<CopyNameConflictModel?> FindNameConflictAsync(EntityCopyPlanModel plan, CancellationToken cancellationToken = default);
    Task CopyAsync(EntityCopyPlanModel plan, CancellationToken cancellationToken = default);
}
