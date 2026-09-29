namespace Straumr.Core.Models;

public sealed record DependencyCopyModel(StraumrModelBase Original, StraumrModelBase Copy, StraumrModelBase? Existing)
{
    public DependencyCopyAction Action { get; set; } = DependencyCopyAction.Create;
    public bool Resolved { get; set; }
    public string Kind => Original is StraumrAuth ? "auth" : "variable";
    public Guid TargetId => Action is DependencyCopyAction.UseExisting or DependencyCopyAction.Replace ? Existing!.Id : Copy.Id;
}
