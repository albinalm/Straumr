using System.Net;
using System.Text;

namespace Straumr.Core.Models;

public class StraumrResponse
{
    public required HttpStatusCode? StatusCode { get; init; }
    public required string? Content { get; init; }
    public byte[]? RawContent { get; init; }
    public long? ContentLength { get; init; }
    public bool BodyOmitted { get; init; }
    public DateTimeOffset? Sent { get; init; }
    public required TimeSpan Duration { get; init; }
    public TimeSpan? TimeToHeaders { get; init; }
    public TimeSpan? BodyDownloadDuration { get; init; }
    public required Exception? Exception { get; init; }

    public IReadOnlyDictionary<string, IEnumerable<string>> ResponseHeaders { get; init; } =
        new Dictionary<string, IEnumerable<string>>();

    public string? ReasonPhrase { get; init; }
    public Version? HttpVersion { get; init; }

    public IReadOnlyDictionary<string, IEnumerable<string>> RequestHeaders { get; internal set; } =
        new Dictionary<string, IEnumerable<string>>();

    public IReadOnlyList<string> Warnings { get; internal set; } = [];

    public long Bytes =>
        ContentLength ?? RawContent?.LongLength ?? Encoding.UTF8.GetByteCount(Content ?? string.Empty);
}
