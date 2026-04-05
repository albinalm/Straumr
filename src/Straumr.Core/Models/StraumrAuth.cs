namespace Straumr.Core.Models;

public class StraumrAuth : StraumrModelBase
{
    public required StraumrAuthConfig Config { get; set; }
    public bool AutoRenewAuth { get; set; } = true;
}
