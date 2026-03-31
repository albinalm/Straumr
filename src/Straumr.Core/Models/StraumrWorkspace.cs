namespace Straumr.Core.Models;

public class StraumrWorkspace : StraumrModelBase
{
    public HashSet<string> Requests { get; set; } = [];
}