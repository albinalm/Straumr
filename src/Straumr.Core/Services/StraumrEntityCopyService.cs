using Straumr.Core.Exceptions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public sealed class StraumrEntityCopyService(
    IStraumrRequestService requests,
    IStraumrAuthService auths,
    IStraumrVariableService variables,
    IStraumrFileService files) : IStraumrEntityCopyService
{
    public async Task<EntityCopyPlanModel> PrepareRequestAsync(StraumrWorkspaceEntry source, Guid id, StraumrWorkspaceEntry destination,
        string name, CancellationToken cancellationToken = default)
    {
        StraumrRequest original = await requests.GetAsync(source, id, false, cancellationToken);
        List<string> missing = [];
        StraumrAuth? auth = null;
        if (original.AuthId is { } authId)
        {
            try { auth = await auths.GetAsync(source, authId, false, cancellationToken); }
            catch (StraumrException exception) when (exception.Reason is StraumrError.EntryNotFound or StraumrError.CorruptEntry)
            {
                missing.Add($"auth {authId.ToString()[..8]}");
            }
        }
        return await PrepareAsync(source, destination, original, original.CopyAs(name), auth, missing, cancellationToken);
    }
    public async Task<EntityCopyPlanModel> PrepareAuthAsync(StraumrWorkspaceEntry source, Guid id, StraumrWorkspaceEntry destination,
        string name, CancellationToken cancellationToken = default)
    {
        StraumrAuth original = await auths.GetAsync(source, id, false, cancellationToken);
        return await PrepareAsync(source, destination, original, original.CopyAs(name), null, [], cancellationToken);
    }
    private async Task<EntityCopyPlanModel> PrepareAsync(StraumrWorkspaceEntry source, StraumrWorkspaceEntry destination,
        StraumrModelBase original, StraumrModelBase copy, StraumrAuth? boundAuth, List<string> missing, CancellationToken cancellationToken)
    {
        StraumrRequest? request = original as StraumrRequest;
        StraumrAuth? referencedAuth = original as StraumrAuth ?? boundAuth;
        List<string> authOnlyMissing = [];
        List<string> requestVariableNames = EntityCopyReferenceHelpers.Names(request, null).ToList();
        IReadOnlyList<StraumrAuth> existingAuths = await auths.ListAsync(destination, cancellationToken);
        IReadOnlyList<StraumrVariable> existingVariables = await variables.ListAsync(destination, cancellationToken);
        List<DependencyCopyModel> copiedVariables = [];
        foreach (string variableName in EntityCopyReferenceHelpers.Names(request, referencedAuth))
        {
            try
            {
                StraumrVariable variable = await variables.GetAsync(source, variableName, false, cancellationToken);
                copiedVariables.Add(new DependencyCopyModel(variable, variable.CopyAs(variable.Name),
                    existingVariables.FirstOrDefault(existing => SameName(existing.Name, variable.Name))));
            }
            catch (StraumrException exception) when (exception.Reason is StraumrError.EntryNotFound or StraumrError.CorruptEntry)
            {
                missing.Add($"variable {variableName}");
                if (!requestVariableNames.Contains(variableName, StringComparer.OrdinalIgnoreCase))
                {
                    authOnlyMissing.Add($"variable {variableName}");
                }
            }
        }
        return new EntityCopyPlanModel(source, destination, original, copy,
            boundAuth is null ? null : new DependencyCopyModel(boundAuth, boundAuth.CopyAs(boundAuth.Name),
                existingAuths.FirstOrDefault(existing => SameName(existing.Name, boundAuth.Name))),
            copiedVariables, requestVariableNames, missing, authOnlyMissing);
    }
    public async Task<CopyNameConflictModel?> FindNameConflictAsync(EntityCopyPlanModel plan, CancellationToken cancellationToken = default)
    {
        IEnumerable<StraumrModelBase> existingEntities = plan.Original is StraumrRequest
            ? await requests.ListAsync(plan.Destination, cancellationToken)
            : await auths.ListAsync(plan.Destination, cancellationToken);
        string? entityProblem = NameProblem(plan.Copy);
        if (entityProblem is not null || existingEntities.Any(existing => SameName(existing.Name, plan.Copy.Name)))
        {
            return new CopyNameConflictModel(null, entityProblem ?? $"The name {plan.Copy.Name} is already used by another {plan.Kind}.");
        }
        if (!plan.CarryDependencies)
        {
            return null;
        }
        IReadOnlyList<StraumrAuth> existingAuths = await auths.ListAsync(plan.Destination, cancellationToken);
        IReadOnlyList<StraumrVariable> existingVariables = await variables.ListAsync(plan.Destination, cancellationToken);
        foreach (DependencyCopyModel dependency in plan.Dependencies)
        {
            if (dependency.Action != DependencyCopyAction.Create)
            {
                StraumrModelBase? existing = dependency.Copy is StraumrAuth
                    ? existingAuths.FirstOrDefault(entity => entity.Id == dependency.Existing!.Id)
                    : existingVariables.FirstOrDefault(entity => entity.Id == dependency.Existing!.Id);
                if (existing is null || !SameName(existing.Name, dependency.Existing!.Name))
                {
                    throw new StraumrException($"The selected {dependency.Kind} changed. Start the copy again.", StraumrError.EntryConflict);
                }
            }
            string? problem = NameProblem(dependency.Copy);
            bool taken = dependency.Action == DependencyCopyAction.Create && (dependency.Copy is StraumrAuth
                ? existingAuths.Any(existing => SameName(existing.Name, dependency.Copy.Name))
                : existingVariables.Any(existing => SameName(existing.Name, dependency.Copy.Name)) ||
                  plan.Variables.Any(other => other != dependency && SameName(other.Copy.Name, dependency.Copy.Name)));
            if (problem is not null || taken)
            {
                return new CopyNameConflictModel(dependency, problem ?? $"A {dependency.Kind} named {dependency.Copy.Name} already exists.");
            }
        }
        return null;
    }
    public async Task CopyAsync(EntityCopyPlanModel plan, CancellationToken cancellationToken = default)
    {
        if (await FindNameConflictAsync(plan, cancellationToken) is { } conflict)
        {
            throw new StraumrException(conflict.Message, StraumrError.EntryConflict);
        }
        if (plan.CarryDependencies && plan.RequiredMissing.Count > 0)
        {
            throw new StraumrException("Repair the missing dependencies before carrying them to another workspace.", StraumrError.MissingEntry);
        }
        Dictionary<string, string?> snapshots = new(StringComparer.OrdinalIgnoreCase);
        snapshots[plan.Destination.Path] = await File.ReadAllTextAsync(plan.Destination.Path, cancellationToken);
        var writes = new List<(StraumrModelBase Model, string Path, string Jsonc, bool Replace)>();
        if (plan.CarryDependencies)
        {
            foreach (DependencyCopyModel dependency in plan.Dependencies.Where(dependency => dependency.Action != DependencyCopyAction.UseExisting))
            {
                StraumrModelBase model = dependency.Copy is StraumrAuth
                    ? EntityCopyReferenceHelpers.Auth(plan, dependency)
                    : ((StraumrVariable)dependency.Original).CopyAs(dependency.Copy.Name);
                model.Id = dependency.TargetId;
                if (dependency.Action == DependencyCopyAction.Replace)
                {
                    model.Name = dependency.Existing!.Name;
                }
                string path = PathFor(plan.Destination, model);
                snapshots[path] = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
                writes.Add((model, path, await File.ReadAllTextAsync(PathFor(plan.Source, dependency.Original), cancellationToken),
                    dependency.Action == DependencyCopyAction.Replace));
            }
        }
        StraumrModelBase entity = plan.Original is StraumrRequest
            ? EntityCopyReferenceHelpers.Request(plan)
            : EntityCopyReferenceHelpers.Auth(plan, (StraumrAuth)plan.Original, plan.Copy.Name, plan.Copy.Id);
        string entityPath = PathFor(plan.Destination, entity);
        snapshots[entityPath] = File.Exists(entityPath) ? await File.ReadAllTextAsync(entityPath, cancellationToken) : null;
        writes.Add((entity, entityPath, await File.ReadAllTextAsync(PathFor(plan.Source, plan.Original), cancellationToken), false));
        try
        {
            foreach ((StraumrModelBase model, string path, string jsonc, bool replace) in writes)
            {
                files.CarryCommentsFrom(path, jsonc);
                await WriteAsync(plan.Destination, model, replace, cancellationToken);
            }
        }
        catch (Exception copyFailure)
        {
            List<Exception> restoreFailures = [];
            foreach ((string path, string? original) in snapshots.OrderBy(snapshot => snapshot.Key == plan.Destination.Path))
            {
                try
                {
                    if (original is null) { File.Delete(path); }
                    else { await File.WriteAllTextAsync(path, original, CancellationToken.None); }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    restoreFailures.Add(exception);
                }
            }
            if (restoreFailures.Count > 0)
            {
                throw new StraumrException("Copy failed and the destination could not be fully restored. Refresh the destination workspace.",
                    StraumrError.CorruptEntry, new AggregateException(new[] { copyFailure }.Concat(restoreFailures)));
            }
            throw;
        }
    }
    private async Task WriteAsync(StraumrWorkspaceEntry destination, StraumrModelBase model, bool replace, CancellationToken token)
    {
        if (model is StraumrAuth auth)
        {
            if (replace) { await auths.SaveAsync(destination, auth, token); }
            else { await auths.CreateAsync(destination, auth, token); }
        }
        else if (model is StraumrVariable variable)
        {
            if (replace) { await variables.SaveAsync(destination, variable, token); }
            else { await variables.CreateAsync(destination, variable, token); }
        }
        else
        {
            await requests.CreateAsync(destination, (StraumrRequest)model, token);
        }
    }
    private string PathFor(StraumrWorkspaceEntry workspace, StraumrModelBase model) => model switch
    {
        StraumrAuth => auths.PathFor(workspace, model.Id),
        StraumrVariable => variables.PathFor(workspace, model.Id),
        _ => requests.PathFor(workspace, model.Id)
    };
    private static string? NameProblem(StraumrModelBase model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return "Enter a name.";
        }
        try
        {
            if (model is StraumrVariable) { StraumrVariableService.ValidateName(model.Name); }
            else if (model is StraumrAuth) { StraumrAuthService.ValidateName(model.Name); }
            else { StraumrRequestService.ValidateName(model.Name); }
            return null;
        }
        catch (StraumrException exception) when (exception.Reason == StraumrError.InvalidEntry)
        {
            return exception.Message;
        }
    }
    private static bool SameName(string left, string right) => left.Equals(right, StringComparison.OrdinalIgnoreCase);
}
