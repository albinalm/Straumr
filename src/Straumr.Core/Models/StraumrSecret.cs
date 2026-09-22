namespace Straumr.Core.Models;

public class StraumrSecret : StraumrModelBase
{
    public required string Value { get; set; }

    public StraumrSecret CopyAs(string name) => new()
    {
        Name = name,
        Value = Value
    };
}
