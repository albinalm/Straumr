namespace Straumr.Core.Models;

public class StraumrVariable : StraumrModelBase
{
    public required string Value { get; set; }

    public StraumrVariable CopyAs(string name) => new()
    {
        Name = name,
        Value = Value
    };
}
