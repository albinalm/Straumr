namespace Straumr.Core.Models;

public sealed class StraumrStoredResponse
{
    public DateTimeOffset Sent { get; set; } = DateTimeOffset.UtcNow;
    public int? StatusCode { get; set; }
    public string? ReasonPhrase { get; set; }
    public string? HttpVersion { get; set; }
    public double DurationMs { get; set; }
    public double? TimeToHeadersMs { get; set; }
    public double? BodyDownloadMs { get; set; }
    public long Bytes { get; set; }
    public string? Body { get; set; }
    public bool BodyOmitted { get; set; }
    public Dictionary<string, List<string>> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; set; } = [];
    public string? Error { get; set; }
}
