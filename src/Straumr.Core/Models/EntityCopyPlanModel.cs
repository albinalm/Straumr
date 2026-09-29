namespace Straumr.Core.Models;

public sealed record EntityCopyPlanModel(
    StraumrWorkspaceEntry Source,
    StraumrWorkspaceEntry Destination,
    StraumrModelBase Original,
    StraumrModelBase Copy,
    DependencyCopyModel? Auth,
    IReadOnlyList<DependencyCopyModel> BoundVariables,
    IReadOnlyList<string> RequestVariableNames,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> AuthOnlyMissing)
{
    public bool CarryDependencies { get; set; }
    public string Kind => Original is StraumrRequest ? "request" : "auth";
    public IReadOnlyList<DependencyCopyModel> Variables => Auth?.Action == DependencyCopyAction.UseExisting
        ? BoundVariables.Where(variable => RequestVariableNames.Contains(variable.Original.Name, StringComparer.OrdinalIgnoreCase)).ToList()
        : BoundVariables;
    public IReadOnlyList<string> RequiredMissing => Auth?.Action == DependencyCopyAction.UseExisting
        ? Missing.Except(AuthOnlyMissing).ToList() : Missing;
    public bool HasDependencies => Auth is not null || Variables.Count > 0 || Missing.Count > 0;
    public IEnumerable<DependencyCopyModel> Dependencies => Auth is null ? Variables : new[] { Auth }.Concat(Variables);
}
