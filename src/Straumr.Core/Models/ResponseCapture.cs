namespace Straumr.Core.Models;

public sealed record ResponseCapture
{
    public required Guid VariableId { get; init; }
    public ExtractionSource ExtractionSource { get; init; } = ExtractionSource.JsonPath;
    public required string Expression { get; init; }
}
