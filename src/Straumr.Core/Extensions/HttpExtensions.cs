using System.Diagnostics;
using System.Text;
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
            byte[] raw = await response.Content.ReadAsByteArrayAsync();
            string body = DecodeBody(raw, response.Content.Headers.ContentType?.CharSet);

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
                RawContent = raw,
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
                RawContent = null,
                Duration = stopwatch.Elapsed,
                Exception = ex,
                StatusCode = null
            };
        }
    }
    private static string DecodeBody(byte[] raw, string? charset)
    {
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                var encoding = Encoding.GetEncoding(charset);
                return encoding.GetString(raw);
            }
            catch (ArgumentException)
            {
                // Fallback below.
            }
        }

        return Encoding.UTF8.GetString(raw);
    }
}
