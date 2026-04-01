using System.Diagnostics;
using Straumr.Core.Models;

namespace Straumr.Core.Extensions;

public static class HttpExtensions
{
    private static readonly Stopwatch Stopwatch = new();

    public static async Task<StraumrResponse> WithMetrics(this Task<HttpResponseMessage> requestTask)
    {
        try
        {
            Stopwatch.Restart();
            HttpResponseMessage response = await requestTask;
            Stopwatch.Stop();
            string body = await response.Content.ReadAsStringAsync();
            return new StraumrResponse
            {
                StatusCode = response.StatusCode,
                Duration = Stopwatch.Elapsed,
                Content = body,
                Exception = null
            };
        }
        catch (Exception ex)
        {
            Stopwatch.Stop();
            return new StraumrResponse
            {
                Content = null,
                Duration = Stopwatch.Elapsed,
                Exception = ex,
                StatusCode = null
            };
        }
    }
}