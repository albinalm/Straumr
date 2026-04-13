using System.Net;
using System.Text;
using Straumr.Core.Models;

namespace Straumr.Console.Tui.Helpers;

public static class SendResultFormatter
{
    private const int SectionUnderlineWidth = 48;

    public static string BuildSummary(
        StraumrRequest request,
        StraumrAuth? auth,
        StraumrResponse response,
        IReadOnlyCollection<string> notes)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"  ▸ Method    {request.Method.Method.ToUpperInvariant()}");
        builder.AppendLine($"  ▸ URL       {request.Uri}");
        builder.AppendLine($"  ▸ Auth      {auth?.Name ?? "None"}");
        builder.AppendLine($"  ▸ Status    {FormatStatus(response)}");
        if (!string.IsNullOrEmpty(response.ReasonPhrase))
        {
            builder.AppendLine($"  ▸ Reason    {response.ReasonPhrase}");
        }

        builder.AppendLine($"  ▸ Duration  {Math.Max(0, response.Duration.TotalMilliseconds):N0} ms");
        if (response.HttpVersion is not null)
        {
            builder.AppendLine($"  ▸ HTTP      {response.HttpVersion}");
        }

        if (notes.Count > 0)
        {
            builder.AppendLine();
            AppendSection(builder, "Notes");
            foreach (string note in notes)
            {
                builder.AppendLine($"  • {note}");
            }
        }

        if (response.Warnings.Count > 0)
        {
            builder.AppendLine();
            AppendSection(builder, "Warnings");
            foreach (string warning in response.Warnings)
            {
                builder.AppendLine($"  • {warning}");
            }
        }

        if (response.Exception is not null)
        {
            builder.AppendLine();
            AppendSection(builder, "Response exception");
            builder.AppendLine($"  {response.Exception.Message}");
        }

        if (response.RequestHeaders.Count > 0)
        {
            builder.AppendLine();
            AppendSection(builder, "Request headers");
            AppendHeaderLines(builder, response.RequestHeaders);
        }

        if (response.ResponseHeaders.Count > 0)
        {
            builder.AppendLine();
            AppendSection(builder, "Response headers");
            AppendHeaderLines(builder, response.ResponseHeaders);
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildRequestTemplate(StraumrRequest request, StraumrResponse response, string body)
    {
        var builder = new StringBuilder();
        string method = request.Method.Method.ToUpperInvariant();
        string http = response.HttpVersion?.ToString() is { Length: > 0 } v ? $"HTTP/{v}" : "HTTP/?";
        builder.AppendLine($"{method} {request.Uri} {http}");

        foreach ((string key, IEnumerable<string> value) in response.RequestHeaders)
        {
            builder.AppendLine($"{key}: {string.Join(", ", value)}");
        }

        builder.AppendLine();
        builder.Append(body);
        return builder.ToString().TrimEnd();
    }

    public static string BuildErrorSummary(string message)
    {
        var builder = new StringBuilder();
        AppendSection(builder, "Error");
        builder.AppendLine("  An error occurred while sending the request.");
        builder.AppendLine();
        builder.AppendLine($"  {message}");
        builder.AppendLine();
        builder.Append("  Press Esc to return and try again.");
        return builder.ToString();
    }

    public static string FormatStatus(StraumrResponse response)
    {
        if (response.StatusCode is { } statusCode)
        {
            string statusName = Enum.IsDefined(typeof(HttpStatusCode), statusCode)
                ? statusCode.ToString()
                : "Unknown";
            return $"{(int)statusCode} {statusName}";
        }

        return response.Exception is not null ? "Error" : "No response";
    }

    private static void AppendSection(StringBuilder builder, string title)
    {
        builder.AppendLine($"── {title} " + new string('─', Math.Max(1, SectionUnderlineWidth - title.Length - 4)));
    }

    private static void AppendHeaderLines(
        StringBuilder builder,
        IReadOnlyDictionary<string, IEnumerable<string>> headers)
    {
        foreach ((string key, IEnumerable<string> value) in headers)
        {
            string joined = string.Join(", ", value);
            builder.AppendLine($"  {key}: {joined}");
        }
    }
}
