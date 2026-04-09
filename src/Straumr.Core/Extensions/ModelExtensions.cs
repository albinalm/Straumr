using System.Net.Http.Headers;
using System.Text;
using Straumr.Core.Enums;
using Straumr.Core.Models;

namespace Straumr.Core.Extensions;

public static class ModelExtensions
{
    public static HttpRequestMessage ToHttpRequestMessage(this StraumrRequest request, StraumrAuthConfig? auth)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        UriBuilder uriBuilder = new UriBuilder(request.Uri);
        if (request.Params.Count > 0)
        {
            string existingQuery = uriBuilder.Query;
            if (!string.IsNullOrEmpty(existingQuery) && existingQuery.StartsWith('?'))
            {
                existingQuery = existingQuery[1..];
            }

            List<string> queryParts = new List<string>();
            if (!string.IsNullOrEmpty(existingQuery))
            {
                queryParts.Add(existingQuery);
            }

            foreach (KeyValuePair<string, string> kv in request.Params)
            {
                string key = Uri.EscapeDataString(kv.Key);
                string value = Uri.EscapeDataString(kv.Value);
                queryParts.Add($"{key}={value}");
            }

            uriBuilder.Query = string.Join('&', queryParts);
        }

        HttpRequestMessage message = new HttpRequestMessage(request.Method, uriBuilder.Uri);

        switch (auth)
        {
            case BearerAuthConfig bearer when !string.IsNullOrWhiteSpace(bearer.Token):
                message.Headers.Authorization = new AuthenticationHeaderValue(
                    string.IsNullOrWhiteSpace(bearer.Prefix) ? "Bearer" : bearer.Prefix, bearer.Token);
                break;
            case BasicAuthConfig basic:
                string credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{basic.Username}:{basic.Password}"));
                message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;
            case OAuth2Config { Token: { } oauthToken } when !string.IsNullOrWhiteSpace(oauthToken.AccessToken):
                string scheme = string.IsNullOrWhiteSpace(oauthToken.TokenType) ? "Bearer" : oauthToken.TokenType;
                message.Headers.Authorization = new AuthenticationHeaderValue(scheme, oauthToken.AccessToken);
                break;
            case CustomAuthConfig { CachedValue: { } cachedValue } custom:
                string headerValue = custom.ApplyHeaderTemplate.Replace("{{value}}", cachedValue);
                message.Headers.TryAddWithoutValidation(custom.ApplyHeaderName, headerValue);
                break;
        }

        List<KeyValuePair<string, string>> pendingContentHeaders = new List<KeyValuePair<string, string>>();

        foreach (KeyValuePair<string, string> header in request.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                pendingContentHeaders.Add(header);
            }
        }

        if (request.BodyType != BodyType.None &&
            request.Bodies.TryGetValue(request.BodyType, out string? body) &&
            !string.IsNullOrWhiteSpace(body))
        {
            switch (request.BodyType)
            {
                case BodyType.Json:
                    message.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    break;
                case BodyType.Xml:
                    message.Content = new StringContent(body, Encoding.UTF8, "application/xml");
                    break;
                case BodyType.Text:
                    message.Content = new StringContent(body, Encoding.UTF8, "text/plain");
                    break;
                case BodyType.FormUrlEncoded:
                    Dictionary<string, string> formPairs =
                        ParseSerializedFields(body).ToDictionary(pair => pair.Key, pair => pair.Value);
                    message.Content = new FormUrlEncodedContent(formPairs);
                    break;
                case BodyType.MultipartForm:
                    MultipartFormDataContent multipart = new MultipartFormDataContent();
                    foreach (KeyValuePair<string, string> field in ParseSerializedFields(body))
                    {
                        if (field.Value.StartsWith('@'))
                        {
                            string path = field.Value[1..];
                            if (!File.Exists(path))
                            {
                                throw new FileNotFoundException($"Multipart file not found: {path}", path);
                            }

                            FileStream fileStream = File.OpenRead(path);
                            StreamContent fileContent = new StreamContent(fileStream);
                            string extension = Path.GetExtension(path);
                            string mime = MimeTypes.GetMimeType(extension);
                            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
                            multipart.Add(fileContent, field.Key, Path.GetFileName(path));
                        }
                        else
                        {
                            multipart.Add(new StringContent(field.Value, Encoding.UTF8), field.Key);
                        }
                    }

                    message.Content = multipart;
                    break;
                case BodyType.Raw:
                    message.Content = new StringContent(body, Encoding.UTF8);
                    message.Content.Headers.ContentType = null;
                    break;
            }
        }

        if (message.Content is not null)
        {
            foreach (KeyValuePair<string, string> header in pendingContentHeaders)
            {
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                    && message.Content.Headers.ContentType is not null)
                {
                    continue;
                }

                message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        else if (pendingContentHeaders.Count > 0)
        {
            message.Content = new ByteArrayContent(Array.Empty<byte>());
            foreach (KeyValuePair<string, string> header in pendingContentHeaders)
            {
                message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return message;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseSerializedFields(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            yield break;
        }

        foreach (string pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                yield return new KeyValuePair<string, string>(Uri.UnescapeDataString(pair), string.Empty);
                continue;
            }

            string key = Uri.UnescapeDataString(pair[..separatorIndex]);
            string value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            yield return new KeyValuePair<string, string>(key, value);
        }
    }
}