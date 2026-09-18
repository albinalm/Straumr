using System.Net;
using Straumr.Core.Models;

namespace Straumr.Core.Extensions;

public static class StoredResponseExtensions
{
    public static StraumrStoredResponse ToStored(this StraumrResponse response, long bodyLimit)
    {
        ArgumentNullException.ThrowIfNull(response);

        long bytes = response.Bytes;
        bool omitted = response.Content is not null && bytes > bodyLimit;
        return new StraumrStoredResponse
        {
            Sent = response.Sent ?? DateTimeOffset.UtcNow,
            StatusCode = (int?)response.StatusCode,
            ReasonPhrase = response.ReasonPhrase,
            HttpVersion = response.HttpVersion?.ToString(),
            DurationMs = response.Duration.TotalMilliseconds,
            TimeToHeadersMs = response.TimeToHeaders?.TotalMilliseconds,
            BodyDownloadMs = response.BodyDownloadDuration?.TotalMilliseconds,
            Bytes = bytes,
            Body = omitted ? null : response.Content,
            BodyOmitted = omitted,
            Headers = response.ResponseHeaders.ToDictionary(
                header => header.Key, header => header.Value.ToList(), StringComparer.OrdinalIgnoreCase),
            Warnings = [.. response.Warnings],
            Error = response.Exception?.Message
        };
    }

    public static StraumrResponse ToResponse(this StraumrStoredResponse stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return new StraumrResponse
        {
            StatusCode = stored.StatusCode is { } code ? (HttpStatusCode)code : null,
            ReasonPhrase = stored.ReasonPhrase,
            HttpVersion = Version.TryParse(stored.HttpVersion, out Version? version) ? version : null,
            Content = stored.Body,
            ContentLength = stored.Bytes,
            BodyOmitted = stored.BodyOmitted,
            Sent = stored.Sent,
            Duration = TimeSpan.FromMilliseconds(stored.DurationMs),
            TimeToHeaders = stored.TimeToHeadersMs is { } headers ? TimeSpan.FromMilliseconds(headers) : null,
            BodyDownloadDuration = stored.BodyDownloadMs is { } download ? TimeSpan.FromMilliseconds(download) : null,
            Exception = stored.Error is { } error ? new HttpRequestException(error) : null,
            ResponseHeaders = stored.Headers.ToDictionary(
                header => header.Key, header => (IEnumerable<string>)header.Value, StringComparer.OrdinalIgnoreCase),
            Warnings = [.. stored.Warnings]
        };
    }
}
