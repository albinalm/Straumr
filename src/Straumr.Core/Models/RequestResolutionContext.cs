namespace Straumr.Core.Models;

internal sealed class RequestResolutionContext(bool allowFetch, SendOptions? options)
{
    public bool AllowFetch { get; } = allowFetch;
    public SendOptions? Options { get; } = options;
    public bool HasVariableLock { get; set; }
    public List<(string Key, string Label)> Chain { get; } = [];
    public List<string> Notices { get; } = [];
}
