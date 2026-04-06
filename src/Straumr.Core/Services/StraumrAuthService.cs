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
    IStraumrOptionsService optionsService,
    IHttpClientFactory httpClientFactory) : IStraumrAuthService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient();

    public async Task<IReadOnlyList<StraumrAuth>> ListAsync()
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        var auths = new List<StraumrAuth>();
        foreach (Guid id in workspace.Auths)
        {
            auths.Add(await PeekByIdAsync(id));
        }

        return auths;
    }

    public async Task<StraumrAuth> GetAsync(string identifier)
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        AuthLookup lookup = await RequireAuthAsync(workspace, identifier,
            $"No auth found with the identifier: {identifier}");

        return await ResolveAuthAsync(lookup);
    }

    public async Task<StraumrAuth> PeekByIdAsync(Guid id)
    {
        GetCurrentWorkspaceEntry();
        string fullPath = AuthPath(id);

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Auth not found", StraumrError.EntryNotFound);
        }

        try
        {
            return await fileService.PeekStraumrModel(fullPath, StraumrJsonContext.Default.StraumrAuth);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid auth", StraumrError.CorruptEntry, jex);
        }
    }

    public async Task CreateAsync(StraumrAuth auth)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        string fullPath = AuthPath(auth.Id);

        if (File.Exists(fullPath))
        {
            throw new StraumrException("Auth already exists", StraumrError.EntryConflict);
        }

        await EnsureNoNameConflictAsync(auth.Name);

        await fileService.WriteStraumrModel(fullPath, auth, StraumrJsonContext.Default.StraumrAuth);
        await AddAuthToWorkspace(entry, auth.Id);
    }

    public async Task UpdateAsync(StraumrAuth auth)
    {
        GetCurrentWorkspaceEntry();
        string fullPath = AuthPath(auth.Id);

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Auth not found", StraumrError.EntryNotFound);
        }

        await EnsureNoNameConflictAsync(auth.Name, auth.Id);

        await fileService.WriteStraumrModel(fullPath, auth, StraumrJsonContext.Default.StraumrAuth);
    }

    public async Task StampAccessAsync(Guid id)
    {
        string fullPath = AuthPath(id);
        await fileService.StampAccessAsync(fullPath, StraumrJsonContext.Default.StraumrAuth);
    }

    public async Task DeleteAsync(string identifier)
    {
        (StraumrWorkspaceEntry entry, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        AuthLookup lookup = await RequireAuthAsync(workspace, identifier, "No auth found");
        RemoveAuthFile(lookup.Id);
        workspace.Auths.Remove(lookup.Id);
        await PersistWorkspaceAsync(entry, workspace);
    }

    public async Task<StraumrAuth> CopyAsync(string identifier, string newName)
    {
        StraumrAuth source = await GetAsync(identifier);
        var copy = new StraumrAuth
        {
            Name = newName,
            Config = source.Config,
            AutoRenewAuth = source.AutoRenewAuth
        };
        await CreateAsync(copy);
        return copy;
    }

    public async Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier)
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        AuthLookup lookup = await RequireAuthAsync(workspace, identifier, "No auth found");
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.Copy(AuthPath(lookup.Id), tempPath, true);
        return (lookup.Id, tempPath);
    }

    public void ApplyEdit(Guid authId, string tempPath)
    {
        File.Copy(tempPath, AuthPath(authId), true);
    }

    public async Task<OAuth2Token> FetchTokenAsync(OAuth2Config config)
    {
        return config.GrantType switch
        {
            OAuth2GrantType.ClientCredentials => await FetchClientCredentialsAsync(config),
            OAuth2GrantType.AuthorizationCode => await FetchAuthorizationCodeAsync(config),
            OAuth2GrantType.ResourceOwnerPassword => await FetchResourceOwnerPasswordAsync(config),
            _ => throw new InvalidOperationException($"Unsupported grant type: {config.GrantType}")
        };
    }

    public async Task<OAuth2Token> EnsureTokenAsync(OAuth2Config config)
    {
        if (config.Token is not null && !config.Token.IsExpired)
        {
            return config.Token;
        }

        if (config.Token?.RefreshToken is not null)
        {
            return await RefreshTokenAsync(config);
        }

        return await FetchTokenAsync(config);
    }

    public async Task<string> ExecuteCustomAuthAsync(CustomAuthConfig config)
    {
        var authRequest = new StraumrRequest
        {
            Name = "__custom_auth__",
            Uri = config.Url,
            Method = new HttpMethod(config.Method),
            Params = new Dictionary<string, string>(config.Params),
            Headers = new Dictionary<string, string>(config.Headers),
            BodyType = config.BodyType,
            Bodies = new Dictionary<BodyType, string>(config.Bodies)
        };

        var httpMessage = authRequest.ToHttpRequestMessage(null);
        HttpResponseMessage response = await _client.SendAsync(httpMessage);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Custom auth request failed ({(int)response.StatusCode} {response.StatusCode}): {errorBody}");
        }

        string body = await response.Content.ReadAsStringAsync();

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

    private async Task<OAuth2Token> RefreshTokenAsync(OAuth2Config config)
    {
        if (config.Token?.RefreshToken is null)
        {
            return await FetchTokenAsync(config);
        }

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = config.Token.RefreshToken,
            ["client_id"] = config.ClientId
        };

        if (!string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            parameters["client_secret"] = config.ClientSecret;
        }

        return await RequestTokenAsync(config.TokenUrl, parameters);
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

    private async Task<OAuth2Token> FetchClientCredentialsAsync(OAuth2Config config)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret
        };

        if (!string.IsNullOrWhiteSpace(config.Scope))
        {
            parameters["scope"] = config.Scope;
        }

        return await RequestTokenAsync(config.TokenUrl, parameters);
    }

    private async Task<OAuth2Token> FetchAuthorizationCodeAsync(OAuth2Config config)
    {
        string? codeVerifier = null;

        var authParams = new Dictionary<string, string>
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
        string code = await ListenForAuthorizationCodeAsync(config.RedirectUri, authUrl, state);

        var tokenParams = new Dictionary<string, string>
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

        return await RequestTokenAsync(config.TokenUrl, tokenParams);
    }

    private async Task<OAuth2Token> FetchResourceOwnerPasswordAsync(OAuth2Config config)
    {
        var parameters = new Dictionary<string, string>
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

        return await RequestTokenAsync(config.TokenUrl, parameters);
    }

    private async Task<OAuth2Token> RequestTokenAsync(string tokenUrl, Dictionary<string, string> parameters)
    {
        using var content = new FormUrlEncodedContent(parameters);
        HttpResponseMessage response = await _client.PostAsync(tokenUrl, content);
        string json = await response.Content.ReadAsStringAsync();

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
        string redirectUri, string authUrl, string expectedState)
    {
        var uri = new Uri(redirectUri);
        var listenerPrefix = $"{uri.Scheme}://{uri.Host}:{uri.Port}/";

        var listener = new HttpListener();
        listener.Prefixes.Add(listenerPrefix);
        listener.Start();

        try
        {
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            HttpListenerContext context = await listener.GetContextAsync();
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
                await context.Response.OutputStream.WriteAsync(errorBuffer);
                context.Response.Close();
                throw new InvalidOperationException($"Authorization failed: {error}");
            }

            if (code is null)
            {
                throw new InvalidOperationException("No authorization code received");
            }

            if (state != expectedState)
            {
                throw new InvalidOperationException("State mismatch — possible CSRF attack");
            }

            responseHtml =
                "<html><body><h2>Authorization successful</h2><p>You can close this window and return to Straumr.</p></body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
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
        var builder = new UriBuilder(baseUrl);
        var queryParts = new List<string>();

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

    private async Task<(StraumrWorkspaceEntry entry, StraumrWorkspace workspace)> LoadWorkspaceAsync()
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        StraumrWorkspace workspace =
            await fileService.PeekStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        return (entry, workspace);
    }

    private StraumrWorkspaceEntry GetCurrentWorkspaceEntry()
    {
        return optionsService.Options.CurrentWorkspace
               ?? throw new StraumrException("No workspace loaded", StraumrError.MissingEntry);
    }

    private async Task AddAuthToWorkspace(StraumrWorkspaceEntry entry, Guid id)
    {
        StraumrWorkspace workspace =
            await fileService.PeekStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        workspace.Auths.Add(id);
        await PersistWorkspaceAsync(entry, workspace);
    }

    private async Task PersistWorkspaceAsync(StraumrWorkspaceEntry entry, StraumrWorkspace workspace)
    {
        await fileService.WriteStraumrModel(entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace);
    }

    private async Task EnsureNoNameConflictAsync(string name, Guid excludeId = default)
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        foreach (Guid id in workspace.Auths)
        {
            if (id == excludeId)
            {
                continue;
            }

            try
            {
                StraumrAuth auth = await PeekByIdAsync(id);
                if (string.Equals(auth.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new StraumrException("An auth with this name already exists", StraumrError.EntryConflict);
                }
            }
            catch (StraumrException ex) when (ex.Reason is StraumrError.CorruptEntry or StraumrError.EntryNotFound) { }
        }
    }

    private string AuthPath(Guid id)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        string? directory = Path.GetDirectoryName(entry.Path);
        return Path.Combine(directory!, $"{id}.json");
    }

    private void RemoveAuthFile(Guid id)
    {
        string path = AuthPath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async Task<StraumrAuth> GetByIdAsync(Guid id)
    {
        GetCurrentWorkspaceEntry();
        string fullPath = AuthPath(id);

        try
        {
            return await fileService.PeekStraumrModel(fullPath, StraumrJsonContext.Default.StraumrAuth);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid auth", StraumrError.CorruptEntry, jex);
        }
    }

    private async Task<AuthLookup?> LookupAuthAsync(StraumrWorkspace workspace, string identifier)
    {
        if (Guid.TryParse(identifier, out Guid authId) && workspace.Auths.Contains(authId))
        {
            return new AuthLookup(authId, null);
        }

        foreach (Guid id in workspace.Auths)
        {
            try
            {
                StraumrAuth auth = await PeekByIdAsync(id);
                if (auth.Name == identifier)
                {
                    return new AuthLookup(id, auth);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<AuthLookup> RequireAuthAsync(
        StraumrWorkspace workspace, string identifier, string errorMessage)
    {
        AuthLookup? lookup = await LookupAuthAsync(workspace, identifier);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }

    private async Task<StraumrAuth> ResolveAuthAsync(AuthLookup lookup)
    {
        return lookup.Auth ?? await GetByIdAsync(lookup.Id);
    }

    private readonly record struct AuthLookup(Guid Id, StraumrAuth? Auth);
}
