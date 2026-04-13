using System.Diagnostics;
using Straumr.Core.Models;

namespace Straumr.Core.Extensions;

public static class HttpExtensions
{
    public static async Task<StraumrResponse> WithMetrics(this Task<HttpResponseMessage> requestTask)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            HttpResponseMessage response = await requestTask;
            stopwatch.Stop();
            string body = await response.Content.ReadAsStringAsync();

            Dictionary<string, IEnumerable<string>> headers = new Dictionary<string, IEnumerable<string>>();
            foreach (KeyValuePair<string, IEnumerable<string>> h in response.Headers)
            {
                headers[h.Key] = h.Value;
            }

            foreach (KeyValuePair<string, IEnumerable<string>> h in response.Content.Headers)
            {
                headers[h.Key] = h.Value;
            }

            return new StraumrResponse
            {
                StatusCode = response.StatusCode,
                Duration = stopwatch.Elapsed,
                Content = body,
                Exception = null,
                ResponseHeaders = headers,
                ReasonPhrase = response.ReasonPhrase,
                HttpVersion = response.Version
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new StraumrResponse
            {
                Content = null,
                Duration = stopwatch.Elapsed,
                Exception = ex,
                StatusCode = null
            };
        }
    }
}