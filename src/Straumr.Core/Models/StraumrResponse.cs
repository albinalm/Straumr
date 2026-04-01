using System.Net;

namespace Straumr.Core.Models;

public class StraumrResponse
{
    public required HttpStatusCode? StatusCode { get; init; }
    public required string? Content { get; init; }
    public required TimeSpan Duration { get; init; }
    public required Exception? Exception { get; init; }
}