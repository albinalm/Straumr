namespace Straumr.Core.Models;

public sealed record VariableSource
{
    public required Guid RequestId { get; init; }
    public ExtractionSource ExtractionSource { get; init; } = ExtractionSource.JsonPath;
    public required string Expression { get; init; }
    public int? TimeToLiveSeconds { get; init; }
}
