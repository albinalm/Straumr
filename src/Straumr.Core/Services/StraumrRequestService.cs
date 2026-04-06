using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Extensions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrRequestService(
    IStraumrFileService fileService,
    IStraumrOptionsService optionsService,
    IHttpClientFactory httpClientFactory,
    IStraumrAuthService authService,
    IStraumrSecretService secretService) : IStraumrRequestService
{
    private static readonly Regex SecretPattern = new(@"\{\{secret:(?<name>[^}]+)\}\}", RegexOptions.Compiled);
    private readonly HttpClient _client = httpClientFactory.CreateClient();

    public async Task<StraumrRequest> GetAsync(string identifier)
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        RequestLookup lookup =
            await RequireRequestAsync(workspace, identifier,
                $"No request found with the identifier: {identifier}");

        return await ResolveRequestAsync(lookup);
    }

    public async Task DeleteAsync(string identifier)
    {
        (StraumrWorkspaceEntry entry, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        RequestLookup lookup = await RequireRequestAsync(workspace, identifier, "No request found");

        await RemoveRequestAsync(entry, workspace, lookup.Id);
    }

    public async Task<StraumrRequest> CopyAsync(string identifier, string newName)
    {
        StraumrRequest source = await GetAsync(identifier);
        var copy = new StraumrRequest
        {
            Name = newName,
            Uri = source.Uri,
            Method = source.Method,
            Params = new Dictionary<string, string>(source.Params, StringComparer.Ordinal),
            Headers = new Dictionary<string, string>(source.Headers, StringComparer.OrdinalIgnoreCase),
            BodyType = source.BodyType,
            Bodies = new Dictionary<BodyType, string>(source.Bodies),
            AuthId = source.AuthId
        };
        await CreateAsync(copy);
        return copy;
    }

    public async Task<StraumrRequest> PeekByIdAsync(Guid id)
    {
        GetCurrentWorkspaceEntry();
        string fullPath = RequestPath(id);

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        try
        {
            return await fileService.PeekStraumrModel(fullPath, StraumrJsonContext.Default.StraumrRequest);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid request", StraumrError.CorruptEntry, jex);
        }
    }

    public async Task<(string ResolvedUrl, IReadOnlyList<string> Warnings)> ResolveUrlAsync(StraumrRequest request)
    {
        var warnings = new List<string>();
        var resolvedSecrets = new Dictionary<string, string>(StringComparer.Ordinal);
        string resolvedUrl = await ResolveSecretReferencesAsync(request.Uri, resolvedSecrets, warnings);
        return (resolvedUrl, warnings);
    }

    public async Task CreateAsync(StraumrRequest request)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();

        string fullPath = RequestPath(request.Id);
        if (File.Exists(fullPath))
        {
            throw new StraumrException("Request already exists", StraumrError.EntryConflict);
        }

        await EnsureNoNameConflictAsync(request.Name);

        await fileService.WriteStraumrModel(fullPath, request, StraumrJsonContext.Default.StraumrRequest);
        await AddRequestToWorkspace(entry, request.Id);
    }

    public async Task UpdateAsync(StraumrRequest request)
    {
        GetCurrentWorkspaceEntry();
        string fullPath = RequestPath(request.Id);

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        await EnsureNoNameConflictAsync(request.Name, request.Id);

        await fileService.WriteStraumrModel(fullPath, request, StraumrJsonContext.Default.StraumrRequest);
    }

    public async Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier)
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        RequestLookup lookup = await RequireRequestAsync(workspace, identifier, "No request found");
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.Copy(RequestPath(lookup.Id), tempPath, true);
        return (lookup.Id, tempPath);
    }

    public void ApplyEdit(Guid requestId, string tempPath)
    {
        File.Copy(tempPath, RequestPath(requestId), true);
    }

    public async Task<StraumrResponse> SendAsync(StraumrRequest request, SendOptions? options = null)
    {
        var warnings = new List<string>();
        var resolvedSecrets = new Dictionary<string, string>(StringComparer.Ordinal);

        StraumrAuth? auth = request.AuthId.HasValue
            ? await authService.PeekByIdAsync(request.AuthId.Value)
            : null;

        StraumrRequest resolvedRequest = await ResolveSecretsAsync(request, resolvedSecrets, warnings);

        StraumrAuthConfig? resolvedAuthConfig = auth is not null
            ? await ResolveAuthSecretsAsync(auth.Config, resolvedSecrets, warnings)
            : null;

        if (auth is not null)
        {
            switch (resolvedAuthConfig)
            {
                case OAuth2Config oauth2 when auth.AutoRenewAuth:
                {
                    OAuth2Token token = await authService.EnsureTokenAsync(oauth2);
                    oauth2.Token = token;
                    ((OAuth2Config)auth.Config).Token = token;
                    await authService.UpdateAsync(auth);
                    break;
                }
                case CustomAuthConfig { CachedValue: null } custom:
                {
                    await authService.ExecuteCustomAuthAsync(custom);
                    ((CustomAuthConfig)auth.Config).CachedValue = custom.CachedValue;
                    await authService.UpdateAsync(auth);
                    break;
                }
            }
        }

        HttpClient client = _client;
        HttpClientHandler? handler = null;

        bool needsCustomHandler = options is { Insecure: true } or { FollowRedirects: true };
        if (needsCustomHandler)
        {
            handler = new HttpClientHandler();
            if (options!.Insecure)
            {
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }

            if (options.FollowRedirects)
            {
                handler.AllowAutoRedirect = true;
            }

            client = new HttpClient(handler, true);
        }

        try
        {
            StraumrResponse response = await SendWithMetadataAsync(client, resolvedRequest, resolvedAuthConfig);
            response.Warnings = warnings;

            if (ShouldRetryCustomAuth(auth, resolvedAuthConfig, response))
            {
                var custom = (CustomAuthConfig)resolvedAuthConfig!;
                custom.CachedValue = null;
                await authService.ExecuteCustomAuthAsync(custom);
                if (auth is not null)
                {
                    ((CustomAuthConfig)auth.Config).CachedValue = custom.CachedValue;
                    await authService.UpdateAsync(auth);
                }

                response = await SendWithMetadataAsync(client, resolvedRequest, resolvedAuthConfig);
                response.Warnings = warnings;
            }

            // Stamp LastAccessed on the request, workspace, and auth (if any).
            // Secrets are already stamped during resolution via secretService.GetAsync.
            StraumrWorkspaceEntry workspaceEntry = GetCurrentWorkspaceEntry();
            await fileService.StampAccessAsync(workspaceEntry.Path, StraumrJsonContext.Default.StraumrWorkspace);
            await fileService.StampAccessAsync(RequestPath(request.Id), StraumrJsonContext.Default.StraumrRequest);
            if (request.AuthId.HasValue)
            {
                await authService.StampAccessAsync(request.AuthId.Value);
            }

            return response;
        }
        finally
        {
            if (handler is not null)
            {
                client.Dispose();
            }
        }
    }

    private async Task EnsureNoNameConflictAsync(string name, Guid excludeId = default)
    {
        (_, StraumrWorkspace workspace) = await LoadWorkspaceAsync();
        foreach (Guid id in workspace.Requests)
        {
            if (id == excludeId)
            {
                continue;
            }

            try
            {
                StraumrRequest request = await PeekByIdAsync(id);
                if (string.Equals(request.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new StraumrException("A request with this name already exists", StraumrError.EntryConflict);
                }
            }
            catch (StraumrException ex) when (ex.Reason is StraumrError.CorruptEntry or StraumrError.EntryNotFound) { }
        }
    }

    private string RequestPath(Guid id)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        string? directory = Path.GetDirectoryName(entry.Path);
        return Path.Combine(directory!, id + ".json");
    }

    private void RemoveRequestFile(Guid id)
    {
        string requestPath = RequestPath(id);
        if (File.Exists(requestPath))
        {
            File.Delete(requestPath);
        }
    }

    private async Task<StraumrRequest> GetByIdAsync(Guid id)
    {
        GetCurrentWorkspaceEntry();
        string fullPath = RequestPath(id);

        try
        {
            return await fileService.ReadStraumrModel(fullPath, StraumrJsonContext.Default.StraumrRequest);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid request", StraumrError.CorruptEntry, jex);
        }
    }

    private StraumrWorkspaceEntry GetCurrentWorkspaceEntry()
    {
        return optionsService.Options.CurrentWorkspace
               ?? throw new StraumrException("No workspace loaded", StraumrError.MissingEntry);
    }

    private async Task AddRequestToWorkspace(StraumrWorkspaceEntry entry, Guid requestId)
    {
        StraumrWorkspace workspace =
            await fileService.PeekStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        workspace.Requests.Add(requestId);
        await PersistWorkspaceAsync(entry, workspace);
    }

    private async Task<(StraumrWorkspaceEntry entry, StraumrWorkspace workspace)> LoadWorkspaceAsync()
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        StraumrWorkspace workspace =
            await fileService.PeekStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        return (entry, workspace);
    }

    private async Task<StraumrRequest> ResolveRequestAsync(RequestLookup lookup)
    {
        return lookup.Request ?? await PeekByIdAsync(lookup.Id);
    }

    private static async Task<StraumrResponse> SendWithMetadataAsync(
        HttpClient client, StraumrRequest request, StraumrAuthConfig? auth)
    {
        var networkRequest = request.ToHttpRequestMessage(auth);
        try
        {
            StraumrResponse response = await client.SendAsync(networkRequest).WithMetrics();
            PopulateRequestMetadata(response, networkRequest);
            return response;
        }
        finally
        {
            networkRequest.Dispose();
        }
    }

    private static void PopulateRequestMetadata(StraumrResponse response, HttpRequestMessage networkRequest)
    {
        var requestHeaders = new Dictionary<string, IEnumerable<string>>();
        foreach (KeyValuePair<string, IEnumerable<string>> h in networkRequest.Headers)
        {
            requestHeaders[h.Key] = h.Value;
        }

        if (networkRequest.Content is not null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> h in networkRequest.Content.Headers)
            {
                requestHeaders[h.Key] = h.Value;
            }
        }

        response.RequestHeaders = requestHeaders;
    }

    private async Task<StraumrRequest> ResolveSecretsAsync(
        StraumrRequest request,
        Dictionary<string, string> resolvedSecrets,
        List<string> warnings)
    {
        return new StraumrRequest
        {
            Id = request.Id,
            Name = request.Name,
            Modified = request.Modified,
            LastAccessed = request.LastAccessed,
            Uri = await ResolveSecretReferencesAsync(request.Uri, resolvedSecrets, warnings),
            Method = request.Method,
            Params = await ResolveSecretReferencesAsync(request.Params, resolvedSecrets, warnings, StringComparer.Ordinal),
            Headers = await ResolveSecretReferencesAsync(request.Headers, resolvedSecrets, warnings, StringComparer.OrdinalIgnoreCase),
            BodyType = request.BodyType,
            Bodies = await ResolveSecretReferencesAsync(request.Bodies, resolvedSecrets, warnings),
            AuthId = request.AuthId
        };
    }

    private async Task<StraumrAuthConfig?> ResolveAuthSecretsAsync(
        StraumrAuthConfig? auth,
        Dictionary<string, string> resolvedSecrets,
        List<string> warnings)
    {
        switch (auth)
        {
            case null:
                return null;
            case BearerAuthConfig bearer:
                return new BearerAuthConfig
                {
                    Token = await ResolveSecretReferencesAsync(bearer.Token, resolvedSecrets, warnings),
                    Prefix = await ResolveSecretReferencesAsync(bearer.Prefix, resolvedSecrets, warnings)
                };
            case BasicAuthConfig basic:
                return new BasicAuthConfig
                {
                    Username = await ResolveSecretReferencesAsync(basic.Username, resolvedSecrets, warnings),
                    Password = await ResolveSecretReferencesAsync(basic.Password, resolvedSecrets, warnings)
                };
            case OAuth2Config oauth2:
                return new OAuth2Config
                {
                    GrantType = oauth2.GrantType,
                    TokenUrl = await ResolveSecretReferencesAsync(oauth2.TokenUrl, resolvedSecrets, warnings),
                    ClientId = await ResolveSecretReferencesAsync(oauth2.ClientId, resolvedSecrets, warnings),
                    ClientSecret = await ResolveSecretReferencesAsync(oauth2.ClientSecret, resolvedSecrets, warnings),
                    Scope = await ResolveSecretReferencesAsync(oauth2.Scope, resolvedSecrets, warnings),
                    AuthorizationUrl = await ResolveSecretReferencesAsync(oauth2.AuthorizationUrl, resolvedSecrets, warnings),
                    RedirectUri = await ResolveSecretReferencesAsync(oauth2.RedirectUri, resolvedSecrets, warnings),
                    UsePkce = oauth2.UsePkce,
                    CodeChallengeMethod = await ResolveSecretReferencesAsync(oauth2.CodeChallengeMethod, resolvedSecrets, warnings),
                    Username = await ResolveSecretReferencesAsync(oauth2.Username, resolvedSecrets, warnings),
                    Password = await ResolveSecretReferencesAsync(oauth2.Password, resolvedSecrets, warnings),
                    Token = oauth2.Token
                };
            case CustomAuthConfig custom:
                return new CustomAuthConfig
                {
                    Url = await ResolveSecretReferencesAsync(custom.Url, resolvedSecrets, warnings),
                    Method = await ResolveSecretReferencesAsync(custom.Method, resolvedSecrets, warnings),
                    BodyType = custom.BodyType,
                    Bodies = await ResolveSecretReferencesAsync(custom.Bodies, resolvedSecrets, warnings),
                    Headers = await ResolveSecretReferencesAsync(custom.Headers, resolvedSecrets, warnings, StringComparer.OrdinalIgnoreCase),
                    Params = await ResolveSecretReferencesAsync(custom.Params, resolvedSecrets, warnings, StringComparer.Ordinal),
                    Source = custom.Source,
                    ExtractionExpression = await ResolveSecretReferencesAsync(custom.ExtractionExpression, resolvedSecrets, warnings),
                    ApplyHeaderName = await ResolveSecretReferencesAsync(custom.ApplyHeaderName, resolvedSecrets, warnings),
                    ApplyHeaderTemplate = await ResolveSecretReferencesAsync(custom.ApplyHeaderTemplate, resolvedSecrets, warnings),
                    CachedValue = custom.CachedValue
                };
            default:
                return auth;
        }
    }

    private async Task<string> ResolveSecretReferencesAsync(
        string value,
        Dictionary<string, string> resolvedSecrets,
        List<string> warnings)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        MatchCollection matches = SecretPattern.Matches(value);
        if (matches.Count == 0)
        {
            return value;
        }

        string resolved = value;
        foreach (Match match in matches)
        {
            string secretName = match.Groups["name"].Value.Trim();
            if (!resolvedSecrets.TryGetValue(secretName, out string? secretValue))
            {
                try
                {
                    StraumrSecret secret = await secretService.GetAsync(secretName);
                    secretValue = secret.Value;
                    resolvedSecrets[secretName] = secretValue;
                }
                catch (StraumrException ex) when (ex.Reason == StraumrError.EntryNotFound)
                {
                    string warning = $"Secret '{secretName}' could not be resolved.";
                    if (!warnings.Contains(warning, StringComparer.Ordinal))
                    {
                        warnings.Add(warning);
                    }

                    continue;
                }
            }

            resolved = resolved.Replace(match.Value, secretValue, StringComparison.Ordinal);
        }

        return resolved;
    }

    private async Task<Dictionary<string, string>> ResolveSecretReferencesAsync(
        Dictionary<string, string> source,
        Dictionary<string, string> resolvedSecrets,
        List<string> warnings,
        IEqualityComparer<string> comparer)
    {
        var resolved = new Dictionary<string, string>(comparer);
        foreach (KeyValuePair<string, string> pair in source)
        {
            resolved[pair.Key] = await ResolveSecretReferencesAsync(pair.Value, resolvedSecrets, warnings);
        }

        return resolved;
    }

    private async Task<Dictionary<BodyType, string>> ResolveSecretReferencesAsync(
        Dictionary<BodyType, string> source,
        Dictionary<string, string> resolvedSecrets,
        List<string> warnings)
    {
        var resolved = new Dictionary<BodyType, string>();
        foreach (KeyValuePair<BodyType, string> pair in source)
        {
            resolved[pair.Key] = await ResolveSecretReferencesAsync(pair.Value, resolvedSecrets, warnings);
        }

        return resolved;
    }

    private static bool ShouldRetryCustomAuth(
        StraumrAuth? auth, StraumrAuthConfig? resolvedAuthConfig, StraumrResponse response)
    {
        return auth is { AutoRenewAuth: true }
               && resolvedAuthConfig is CustomAuthConfig
               && response.StatusCode == HttpStatusCode.Unauthorized;
    }

    private async Task RemoveRequestAsync(StraumrWorkspaceEntry entry, StraumrWorkspace workspace, Guid id)
    {
        RemoveRequestFile(id);
        workspace.Requests.Remove(id);
        await PersistWorkspaceAsync(entry, workspace);
    }

    private async Task PersistWorkspaceAsync(StraumrWorkspaceEntry entry, StraumrWorkspace workspace)
    {
        await fileService.WriteStraumrModel(entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace);
    }

    private async Task<RequestLookup?> LookupRequestAsync(StraumrWorkspace workspace, string identifier)
    {
        if (Guid.TryParse(identifier, out Guid requestId) && workspace.Requests.Contains(requestId))
        {
            return new RequestLookup(requestId, null);
        }

        foreach (Guid id in workspace.Requests)
        {
            try
            {
                StraumrRequest request = await PeekByIdAsync(id);
                if (request.Name == identifier)
                {
                    return new RequestLookup(id, request);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<RequestLookup> RequireRequestAsync(StraumrWorkspace workspace, string identifier,
        string errorMessage)
    {
        RequestLookup? lookup = await LookupRequestAsync(workspace, identifier);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }

    private readonly record struct RequestLookup(Guid Id, StraumrRequest? Request);
}
