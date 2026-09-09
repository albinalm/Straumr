using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Extensions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrAuthService(
    IStraumrFileService fileService,
    IHttpClientFactory httpClientFactory) : IStraumrAuthService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient();

    public async Task<IReadOnlyList<StraumrAuth>> ListAsync(
        StraumrWorkspaceEntry workspace,
        CancellationToken cancellationToken = default)
    {
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        List<StraumrAuth> auths = new List<StraumrAuth>();
        foreach (Guid id in workspaceModel.Auths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            auths.Add(await ReadByIdAsync(workspace, id, updateLastAccessed: false, cancellationToken));
        }

        return auths;
    }

    public async Task<StraumrAuth> GetAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        if (!workspaceModel.Auths.Contains(id))
        {
            throw new StraumrException("Auth not found", StraumrError.EntryNotFound);
        }

        return await ReadByIdAsync(workspace, id, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrAuth> GetAsync(
        StraumrWorkspaceEntry workspace,
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        AuthLookup lookup = await RequireAuthAsync(
            workspaceModel, name, $"No auth found with the name: {name}", workspace, cancellationToken);
        return await ResolveAuthAsync(lookup, workspace, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrAuth> CreateAsync(
        StraumrWorkspaceEntry workspace,
        StraumrAuth auth,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = AuthPath(auth.Id, workspace);

        if (File.Exists(fullPath))
        {
            throw new StraumrException("Auth already exists", StraumrError.EntryConflict);
        }

        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        await EnsureNoNameConflictAsync(auth.Name, workspace, workspaceModel: workspaceModel,
            cancellationToken: cancellationToken);

        await fileService.WriteStraumrModelAsync(
            fullPath, auth, StraumrJsonContext.Default.StraumrAuth, cancellationToken);
        workspaceModel.Auths.Add(auth.Id);
        await PersistWorkspaceAsync(workspace, workspaceModel, cancellationToken);
        return auth;
    }

    public async Task<StraumrAuth> SaveAsync(
        StraumrWorkspaceEntry workspace,
        StraumrAuth auth,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = AuthPath(auth.Id, workspace);

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Auth not found", StraumrError.EntryNotFound);
        }

        await EnsureNoNameConflictAsync(
            auth.Name, workspace, auth.Id, cancellationToken: cancellationToken);

        await fileService.WriteStraumrModelAsync(
            fullPath, auth, StraumrJsonContext.Default.StraumrAuth, cancellationToken);
        return auth;
    }

    public async Task DeleteAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        if (!workspaceModel.Auths.Contains(id))
        {
            throw new StraumrException("Auth not found", StraumrError.EntryNotFound);
        }

        string authPath = AuthPath(id, workspace);
        if (File.Exists(authPath))
        {
            File.Delete(authPath);
        }
        workspaceModel.Auths.Remove(id);
        await PersistWorkspaceAsync(workspace, workspaceModel, cancellationToken);
    }

    public async Task<OAuth2Token> FetchTokenAsync(
        OAuth2Config config,
        CancellationToken cancellationToken = default)
    {
        return config.GrantType switch
        {
            OAuth2GrantType.ClientCredentials => await FetchClientCredentialsAsync(config, cancellationToken),
            OAuth2GrantType.AuthorizationCode => await FetchAuthorizationCodeAsync(config, cancellationToken),
            OAuth2GrantType.ResourceOwnerPassword => await FetchResourceOwnerPasswordAsync(config, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported grant type: {config.GrantType}")
        };
    }

    public async Task<OAuth2Token> EnsureTokenAsync(
        OAuth2Config config,
        CancellationToken cancellationToken = default)
    {
        if (config.Token is not null && !config.Token.IsExpired)
        {
            return config.Token;
        }

        if (config.Token?.RefreshToken is not null)
        {
            return await RefreshTokenAsync(config, cancellationToken);
        }

        return await FetchTokenAsync(config, cancellationToken);
    }

    public async Task<string> ExecuteCustomAuthAsync(
        CustomAuthConfig config,
        CancellationToken cancellationToken = default)
    {
        StraumrRequest authRequest = new StraumrRequest
        {
            Name = "__custom_auth__",
            Uri = config.Url,
            Method = new HttpMethod(config.Method),
            Params = new Dictionary<string, string>(config.Params),
            Headers = new Dictionary<string, string>(config.Headers),
            BodyType = config.BodyType,
            Bodies = new Dictionary<BodyType, string>(config.Bodies)
        };

        using HttpRequestMessage httpMessage = authRequest.ToHttpRequestMessage(null);
        using HttpResponseMessage response = await _client.SendAsync(
            httpMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Custom auth request failed ({(int)response.StatusCode} {response.StatusCode}): {errorBody}");
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        string extracted = config.Source switch
        {
            ExtractionSource.JsonPath => ExtractFromJson(body, config.ExtractionExpression),
            ExtractionSource.ResponseHeader => ExtractFromHeader(response, config.ExtractionExpression),
            ExtractionSource.Regex => ExtractFromRegex(body, config.ExtractionExpression),
            _ => throw new InvalidOperationException($"Unsupported extraction source: {config.Source}")
        };

        config.CachedValue = extracted;
        return extracted;
    }

    private async Task<OAuth2Token> RefreshTokenAsync(
        OAuth2Config config,
        CancellationToken cancellationToken)
    {
        if (config.Token?.RefreshToken is null)
        {
            return await FetchTokenAsync(config, cancellationToken);
        }

        Dictionary<string, string> parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = config.Token.RefreshToken,
            ["client_id"] = config.ClientId
        };

        if (!string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            parameters["client_secret"] = config.ClientSecret;
        }

        return await RequestTokenAsync(config.TokenUrl, parameters, cancellationToken);
    }

    private static string ExtractFromJson(string json, string path)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement current = doc.RootElement;

        foreach (string segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out int index))
            {
                if (index < 0 || index >= current.GetArrayLength())
                {
                    throw new InvalidOperationException($"Array index [{index}] out of range in JSON path: {path}");
                }

                current = current[index];
            }
            else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out JsonElement next))
            {
                current = next;
            }
            else
            {
                throw new InvalidOperationException($"Property '{segment}' not found in JSON path: {path}");
            }
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString() ?? string.Empty
            : current.GetRawText();
    }

    private static string ExtractFromHeader(HttpResponseMessage response, string headerName)
    {
        if (response.Headers.TryGetValues(headerName, out IEnumerable<string>? values))
        {
            return values.First();
        }

        if (response.Content.Headers.TryGetValues(headerName, out IEnumerable<string>? contentValues))
        {
            return contentValues.First();
        }

        throw new InvalidOperationException($"Response header '{headerName}' not found");
    }

    private static string ExtractFromRegex(string body, string pattern)
    {
        Match match = Regex.Match(body, pattern);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Regex pattern did not match: {pattern}");
        }

        return match.Groups.Count > 1
            ? match.Groups[1].Value
            : match.Value;
    }

    private async Task<OAuth2Token> FetchClientCredentialsAsync(
        OAuth2Config config,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret
        };

        if (!string.IsNullOrWhiteSpace(config.Scope))
        {
            parameters["scope"] = config.Scope;
        }

        return await RequestTokenAsync(config.TokenUrl, parameters, cancellationToken);
    }

    private async Task<OAuth2Token> FetchAuthorizationCodeAsync(
        OAuth2Config config,
        CancellationToken cancellationToken)
    {
        string? codeVerifier = null;

        Dictionary<string, string> authParams = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = config.ClientId,
            ["redirect_uri"] = config.RedirectUri
        };

        if (!string.IsNullOrWhiteSpace(config.Scope))
        {
            authParams["scope"] = config.Scope;
        }

        string state = GenerateRandomString(32);
        authParams["state"] = state;

        if (config.UsePkce)
        {
            codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(codeVerifier, config.CodeChallengeMethod);
            authParams["code_challenge"] = codeChallenge;
            authParams["code_challenge_method"] = config.CodeChallengeMethod;
        }

        string authUrl = BuildUrlWithParams(config.AuthorizationUrl, authParams);
        string code = await ListenForAuthorizationCodeAsync(
            config.RedirectUri, authUrl, state, cancellationToken);

        Dictionary<string, string> tokenParams = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = config.RedirectUri,
            ["client_id"] = config.ClientId
        };

        if (!string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            tokenParams["client_secret"] = config.ClientSecret;
        }

        if (codeVerifier is not null)
        {
            tokenParams["code_verifier"] = codeVerifier;
        }

        return await RequestTokenAsync(config.TokenUrl, tokenParams, cancellationToken);
    }

    private async Task<OAuth2Token> FetchResourceOwnerPasswordAsync(
        OAuth2Config config,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = config.ClientId,
            ["username"] = config.Username,
            ["password"] = config.Password
        };

        if (!string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            parameters["client_secret"] = config.ClientSecret;
        }

        if (!string.IsNullOrWhiteSpace(config.Scope))
        {
            parameters["scope"] = config.Scope;
        }

        return await RequestTokenAsync(config.TokenUrl, parameters, cancellationToken);
    }

    private async Task<OAuth2Token> RequestTokenAsync(
        string tokenUrl,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        using FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
        using HttpResponseMessage response = await _client.PostAsync(tokenUrl, content, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Token request failed ({(int)response.StatusCode} {response.StatusCode}): {json}");
        }

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string accessToken = root.GetProperty("access_token").GetString()
                             ?? throw new InvalidOperationException("Token response missing access_token");

        string tokenType = root.TryGetProperty("token_type", out JsonElement tt)
            ? tt.GetString() ?? "Bearer"
            : "Bearer";

        string? refreshToken = root.TryGetProperty("refresh_token", out JsonElement rt)
            ? rt.GetString()
            : null;

        DateTimeOffset? expiresAt = null;
        if (root.TryGetProperty("expires_in", out JsonElement ei) && ei.TryGetInt32(out int expiresIn))
        {
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        }

        return new OAuth2Token
        {
            AccessToken = accessToken,
            TokenType = tokenType,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        };
    }

    private static async Task<string> ListenForAuthorizationCodeAsync(
        string redirectUri,
        string authUrl,
        string expectedState,
        CancellationToken cancellationToken)
    {
        Uri uri = new Uri(redirectUri);
        string listenerPrefix = $"{uri.Scheme}://{uri.Host}:{uri.Port}/";

        HttpListener listener = new HttpListener();
        listener.Prefixes.Add(listenerPrefix);
        listener.Start();

        try
        {
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            string? code = context.Request.QueryString["code"];
            string? state = context.Request.QueryString["state"];
            string? error = context.Request.QueryString["error"];

            string responseHtml;
            if (error is not null)
            {
                responseHtml =
                    $"<html><body><h2>Authorization failed</h2><p>{WebUtility.HtmlEncode(error)}</p><p>You can close this window.</p></body></html>";
                byte[] errorBuffer = Encoding.UTF8.GetBytes(responseHtml);
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = errorBuffer.Length;
                await context.Response.OutputStream.WriteAsync(errorBuffer, cancellationToken);
                context.Response.Close();
                throw new InvalidOperationException($"Authorization failed: {error}");
            }

            if (code is null)
            {
                throw new InvalidOperationException("No authorization code received");
            }

            if (state != expectedState)
            {
                throw new InvalidOperationException("State mismatch - possible CSRF attack");
            }

            responseHtml =
                "<html><body><h2>Authorization successful</h2><p>You can close this window and return to Straumr.</p></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
            context.Response.Close();

            return code;
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    private static string GenerateRandomString(int length)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=')[..length];
    }

    private static string GenerateCodeVerifier()
    {
        return GenerateRandomString(64);
    }

    private static string GenerateCodeChallenge(string codeVerifier, string method)
    {
        if (method == "plain")
        {
            return codeVerifier;
        }

        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string BuildUrlWithParams(string baseUrl, Dictionary<string, string> parameters)
    {
        UriBuilder builder = new UriBuilder(baseUrl);
        List<string> queryParts = new List<string>();

        if (!string.IsNullOrEmpty(builder.Query) && builder.Query.Length > 1)
        {
            queryParts.Add(builder.Query[1..]);
        }

        foreach (KeyValuePair<string, string> kv in parameters)
        {
            queryParts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}");
        }

        builder.Query = string.Join('&', queryParts);
        return builder.Uri.ToString();
    }

    private async Task<StraumrWorkspace> LoadWorkspaceAsync(
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken = default)
    {
        return await fileService.PeekStraumrModelAsync(
            entry.Path, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);
    }

    private async Task PersistWorkspaceAsync(
        StraumrWorkspaceEntry entry,
        StraumrWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        await fileService.WriteStraumrModelAsync(
            entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);
    }

    private async Task EnsureNoNameConflictAsync(
        string name,
        StraumrWorkspaceEntry entry,
        Guid excludeId = default,
        StraumrWorkspace? workspaceModel = null,
        CancellationToken cancellationToken = default)
    {
        StraumrWorkspace workspace = workspaceModel ?? await LoadWorkspaceAsync(entry, cancellationToken);
        foreach (Guid id in workspace.Auths)
        {
            if (id == excludeId)
            {
                continue;
            }

            try
            {
                StraumrAuth auth = await ReadByIdAsync(
                    entry, id, updateLastAccessed: false, cancellationToken);
                if (string.Equals(auth.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new StraumrException("An auth with this name already exists", StraumrError.EntryConflict);
                }
            }
            catch (StraumrException ex) when (ex.Reason is StraumrError.CorruptEntry or StraumrError.EntryNotFound) { }
        }
    }

    private static string AuthPath(Guid id, StraumrWorkspaceEntry entry)
    {
        string? directory = Path.GetDirectoryName(entry.Path);
        return Path.Combine(directory!, $"{id}.json");
    }

    private async Task<StraumrAuth> ReadByIdAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        bool updateLastAccessed,
        CancellationToken cancellationToken = default)
    {
        string fullPath = AuthPath(id, workspace);
        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Auth not found", StraumrError.EntryNotFound);
        }

        try
        {
            return updateLastAccessed
                ? await fileService.ReadStraumrModelAsync(
                    fullPath, StraumrJsonContext.Default.StraumrAuth, cancellationToken)
                : await fileService.PeekStraumrModelAsync(
                    fullPath, StraumrJsonContext.Default.StraumrAuth, cancellationToken);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid auth", StraumrError.CorruptEntry, jex);
        }
    }

    private async Task<AuthLookup?> LookupAuthAsync(
        StraumrWorkspace workspace,
        string name,
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken)
    {
        foreach (Guid id in workspace.Auths)
        {
            try
            {
                StraumrAuth auth = await ReadByIdAsync(
                    entry, id, updateLastAccessed: false, cancellationToken);
                if (string.Equals(auth.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new AuthLookup(id, auth);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<AuthLookup> RequireAuthAsync(
        StraumrWorkspace workspace, string name, string errorMessage, StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken)
    {
        AuthLookup? lookup = await LookupAuthAsync(workspace, name, entry, cancellationToken);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }

    private async Task<StraumrAuth> ResolveAuthAsync(
        AuthLookup lookup,
        StraumrWorkspaceEntry workspace,
        bool updateLastAccessed,
        CancellationToken cancellationToken)
    {
        if (updateLastAccessed)
        {
            await fileService.StampAccessAsync(
                AuthPath(lookup.Id, workspace), lookup.Auth,
                StraumrJsonContext.Default.StraumrAuth, cancellationToken);
        }

        return lookup.Auth;
    }

    private readonly record struct AuthLookup(Guid Id, StraumrAuth Auth);
}
