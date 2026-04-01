using System.Net;

namespace Straumr.Core.Models;

public class StraumrResponse
{
    public required HttpStatusCode? StatusCode { get; init; }
    public required string? Content { get; init; }
    public required TimeSpan Duration { get; init; }
    public required Exception? Exception { get; init; }

    public IReadOnlyDictionary<string, IEnumerable<string>> ResponseHeaders { get; init; } =
        new Dictionary<string, IEnumerable<string>>();

    public string? ReasonPhrase { get; init; }
    public Version? HttpVersion { get; init; }

    public IReadOnlyDictionary<string, IEnumerable<string>> RequestHeaders { get; internal set; } =
        new Dictionary<string, IEnumerable<string>>();
}