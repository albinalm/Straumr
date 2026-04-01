namespace Straumr.Core.Models;

public class StraumrWorkspace : StraumrModelBase
{
    public HashSet<Guid> Requests { get; set; } = [];
    public HashSet<Guid> AuthTemplates { get; set; } = [];
}