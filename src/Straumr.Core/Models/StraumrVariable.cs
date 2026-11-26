namespace Straumr.Core.Models;

public class StraumrVariable : StraumrModelBase
{
    public required string Value { get; set; }
    public VariableSource? Source { get; set; }
    public string? CachedValue { get; set; }
    public DateTimeOffset? CachedAt { get; set; }
    public string? CacheKey { get; set; }

    public StraumrVariable CopyAs(string name) => new()
    {
        Name = name,
        Value = Value,
        Source = Source
    };
}
