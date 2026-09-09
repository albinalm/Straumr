using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Extensions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;
using Straumr.Core.Helpers;

namespace Straumr.Core.Services;

public class StraumrRequestService(
    IStraumrFileService fileService,
    IHttpClientFactory httpClientFactory,
    IStraumrAuthService authService,
    IStraumrSecretService secretService) : IStraumrRequestService
{
    private static readonly Regex SecretPattern = SecretHelpers.SecretPattern;
    public async Task<IReadOnlyList<StraumrRequest>> ListAsync(
        StraumrWorkspaceEntry workspace,
        CancellationToken cancellationToken = default)
    {
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        List<StraumrRequest> requests = new List<StraumrRequest>();
        foreach (Guid id in workspaceModel.Requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            requests.Add(await ReadByIdAsync(workspace, id, updateLastAccessed: false, cancellationToken));
        }

        return requests;
    }

    public async Task<StraumrRequest> GetAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        if (!workspaceModel.Requests.Contains(id))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        return await ReadByIdAsync(workspace, id, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrRequest> GetAsync(
        StraumrWorkspaceEntry workspace,
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        RequestLookup lookup = await RequireRequestAsync(
            workspaceModel, name, $"No request found with the name: {name}", workspace, cancellationToken);
        return await ResolveRequestAsync(lookup, workspace, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrRequest> CreateAsync(
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = RequestPath(request.Id, workspace);
        if (File.Exists(fullPath))
        {
            throw new StraumrException("Request already exists", StraumrError.EntryConflict);
        }

        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        await EnsureNoNameConflictAsync(request.Name, workspace, workspaceModel: workspaceModel,
            cancellationToken: cancellationToken);
        await fileService.WriteStraumrModelAsync(
            fullPath, request, StraumrJsonContext.Default.StraumrRequest, cancellationToken);
        workspaceModel.Requests.Add(request.Id);
        await PersistWorkspaceAsync(workspace, workspaceModel, cancellationToken);
        return request;
    }

    public async Task<StraumrRequest> SaveAsync(
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = RequestPath(request.Id, workspace);
        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        await EnsureNoNameConflictAsync(
            request.Name, workspace, request.Id, cancellationToken: cancellationToken);
        await fileService.WriteStraumrModelAsync(
            fullPath, request, StraumrJsonContext.Default.StraumrRequest, cancellationToken);
        return request;
    }

    public async Task DeleteAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        if (!workspaceModel.Requests.Contains(id))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        await RemoveRequestAsync(workspace, workspaceModel, id, cancellationToken);
    }

    public async Task<(string ResolvedUrl, IReadOnlyList<string> Warnings)> ResolveUrlAsync(
        StraumrRequest request,
        CancellationToken cancellationToken = default)
    {
        List<string> warnings = new List<string>();
        Dictionary<string, string> resolvedSecrets = new Dictionary<string, string>(StringComparer.Ordinal);
        string resolvedUrl = await ResolveSecretReferencesAsync(
            request.Uri, resolvedSecrets, warnings, cancellationToken);
        return (resolvedUrl, warnings);
    }

    public async Task<StraumrResponse> SendAsync(
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        SendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        List<string> warnings = new List<string>();
        Dictionary<string, string> resolvedSecrets = new Dictionary<string, string>(StringComparer.Ordinal);

        StraumrAuth? auth = request.AuthId.HasValue
            ? await authService.GetAsync(
                workspace, request.AuthId.Value, updateLastAccessed: true, cancellationToken)
            : null;

        StraumrRequest resolvedRequest = await ResolveSecretsAsync(
            request, resolvedSecrets, warnings, cancellationToken);

        StraumrAuthConfig? resolvedAuthConfig = auth is not null
            ? await ResolveAuthSecretsAsync(auth.Config, resolvedSecrets, warnings, cancellationToken)
            : null;

        if (auth is not null)
        {
            switch (resolvedAuthConfig)
            {
                case OAuth2Config oauth2 when auth.AutoRenewAuth:
                    {
                        bool tokenChanged = oauth2.Token is null || oauth2.Token.IsExpired;
                        OAuth2Token token = await authService.EnsureTokenAsync(oauth2, cancellationToken);
                        oauth2.Token = token;
                        if (tokenChanged)
                        {
                            ((OAuth2Config)auth.Config).Token = token;
                            await authService.SaveAsync(workspace, auth, cancellationToken);
                        }

                        break;
                    }
                case CustomAuthConfig { CachedValue: null } custom:
                    {
                        await authService.ExecuteCustomAuthAsync(custom, cancellationToken);
                        ((CustomAuthConfig)auth.Config).CachedValue = custom.CachedValue;
                        await authService.SaveAsync(workspace, auth, cancellationToken);
                        break;
                    }
            }
        }

        using HttpClient client = httpClientFactory.CreateClient(StraumrHttpClientNames.For(options));

        StraumrResponse response = await SendWithMetadataAsync(
            client, resolvedRequest, resolvedAuthConfig, cancellationToken);
        response.Warnings = warnings;

        if (ShouldRetryCustomAuth(auth, resolvedAuthConfig, response))
        {
            CustomAuthConfig custom = (CustomAuthConfig)resolvedAuthConfig!;
            custom.CachedValue = null;
            await authService.ExecuteCustomAuthAsync(custom, cancellationToken);
            if (auth is not null)
            {
                ((CustomAuthConfig)auth.Config).CachedValue = custom.CachedValue;
                await authService.SaveAsync(workspace, auth, cancellationToken);
            }

            response = await SendWithMetadataAsync(
                client, resolvedRequest, resolvedAuthConfig, cancellationToken);
            response.Warnings = warnings;
        }

        List<Task> accessStamps =
        [
            fileService.StampAccessAsync(
                workspace.Path, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken),
            fileService.StampAccessAsync(
                RequestPath(request.Id, workspace), StraumrJsonContext.Default.StraumrRequest, cancellationToken)
        ];

        await Task.WhenAll(accessStamps);
        return response;
    }

    private async Task EnsureNoNameConflictAsync(
        string name,
        StraumrWorkspaceEntry entry,
        Guid excludeId = default,
        StraumrWorkspace? workspaceModel = null,
        CancellationToken cancellationToken = default)
    {
        StraumrWorkspace workspace = workspaceModel ?? await LoadWorkspaceAsync(entry, cancellationToken);
        foreach (Guid id in workspace.Requests)
        {
            if (id == excludeId)
            {
                continue;
            }

            try
            {
                StraumrRequest request = await ReadByIdAsync(
                    entry, id, updateLastAccessed: false, cancellationToken);
                if (string.Equals(request.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new StraumrException("A request with this name already exists", StraumrError.EntryConflict);
                }
            }
            catch (StraumrException ex) when (ex.Reason is StraumrError.CorruptEntry or StraumrError.EntryNotFound) { }
        }
    }

    private static string RequestPath(Guid id, StraumrWorkspaceEntry entry)
    {
        string? directory = Path.GetDirectoryName(entry.Path);
        return Path.Combine(directory!, id + ".json");
    }

    private async Task<StraumrWorkspace> LoadWorkspaceAsync(
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken = default)
    {
        return await fileService.PeekStraumrModelAsync(
            entry.Path, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);
    }

    private async Task<StraumrRequest> ReadByIdAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        bool updateLastAccessed,
        CancellationToken cancellationToken = default)
    {
        string path = RequestPath(id, workspace);
        if (!File.Exists(path))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        try
        {
            return updateLastAccessed
                ? await fileService.ReadStraumrModelAsync(
                    path, StraumrJsonContext.Default.StraumrRequest, cancellationToken)
                : await fileService.PeekStraumrModelAsync(
                    path, StraumrJsonContext.Default.StraumrRequest, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new StraumrException("Invalid request", StraumrError.CorruptEntry, exception);
        }
    }

    private async Task<StraumrRequest> ResolveRequestAsync(
        RequestLookup lookup,
        StraumrWorkspaceEntry workspace,
        bool updateLastAccessed,
        CancellationToken cancellationToken)
    {
        if (updateLastAccessed)
        {
            await fileService.StampAccessAsync(
                RequestPath(lookup.Id, workspace), lookup.Request,
                StraumrJsonContext.Default.StraumrRequest, cancellationToken);
        }

        return lookup.Request;
    }

    private static async Task<StraumrResponse> SendWithMetadataAsync(
        HttpClient client,
        StraumrRequest request,
        StraumrAuthConfig? auth,
        CancellationToken cancellationToken)
    {
        HttpRequestMessage networkRequest = request.ToHttpRequestMessage(auth);
        try
        {
            StraumrResponse response = await client
                .SendAsync(networkRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .WithMetrics(cancellationToken);
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
        Dictionary<string, IEnumerable<string>> requestHeaders = new Dictionary<string, IEnumerable<string>>();
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
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        return new StraumrRequest
        {
            Id = request.Id,
            Name = request.Name,
            Modified = request.Modified,
            LastAccessed = request.LastAccessed,
            Uri = await ResolveSecretReferencesAsync(request.Uri, resolvedSecrets, warnings, cancellationToken),
            Method = request.Method,
            Params = await ResolveSecretReferencesAsync(request.Params, resolvedSecrets, warnings,
                StringComparer.Ordinal, cancellationToken),
            Headers = await ResolveSecretReferencesAsync(request.Headers, resolvedSecrets, warnings,
                StringComparer.OrdinalIgnoreCase, cancellationToken),
            BodyType = request.BodyType,
            Bodies = await ResolveSecretReferencesAsync(request.Bodies, resolvedSecrets, warnings, cancellationToken),
            AuthId = request.AuthId
        };
    }

    private async Task<StraumrAuthConfig?> ResolveAuthSecretsAsync(
        StraumrAuthConfig? auth,
        Dictionary<string, string> resolvedSecrets,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        switch (auth)
        {
            case null:
                return null;
            case BearerAuthConfig bearer:
                return new BearerAuthConfig
                {
                    Token = await ResolveSecretReferencesAsync(bearer.Token, resolvedSecrets, warnings, cancellationToken),
                    Prefix = await ResolveSecretReferencesAsync(bearer.Prefix, resolvedSecrets, warnings, cancellationToken)
                };
            case BasicAuthConfig basic:
                return new BasicAuthConfig
                {
                    Username = await ResolveSecretReferencesAsync(basic.Username, resolvedSecrets, warnings, cancellationToken),
                    Password = await ResolveSecretReferencesAsync(basic.Password, resolvedSecrets, warnings, cancellationToken)
                };
            case OAuth2Config oauth2:
                return new OAuth2Config
                {
                    GrantType = oauth2.GrantType,
                    TokenUrl = await ResolveSecretReferencesAsync(oauth2.TokenUrl, resolvedSecrets, warnings, cancellationToken),
                    ClientId = await ResolveSecretReferencesAsync(oauth2.ClientId, resolvedSecrets, warnings, cancellationToken),
                    ClientSecret = await ResolveSecretReferencesAsync(oauth2.ClientSecret, resolvedSecrets, warnings, cancellationToken),
                    Scope = await ResolveSecretReferencesAsync(oauth2.Scope, resolvedSecrets, warnings, cancellationToken),
                    AuthorizationUrl = await ResolveSecretReferencesAsync(oauth2.AuthorizationUrl, resolvedSecrets, warnings, cancellationToken),
                    RedirectUri = await ResolveSecretReferencesAsync(oauth2.RedirectUri, resolvedSecrets, warnings, cancellationToken),
                    UsePkce = oauth2.UsePkce,
                    CodeChallengeMethod = await ResolveSecretReferencesAsync(oauth2.CodeChallengeMethod, resolvedSecrets, warnings, cancellationToken),
                    Username = await ResolveSecretReferencesAsync(oauth2.Username, resolvedSecrets, warnings, cancellationToken),
                    Password = await ResolveSecretReferencesAsync(oauth2.Password, resolvedSecrets, warnings, cancellationToken),
                    Token = oauth2.Token
                };
            case CustomAuthConfig custom:
                return new CustomAuthConfig
                {
                    Url = await ResolveSecretReferencesAsync(custom.Url, resolvedSecrets, warnings, cancellationToken),
                    Method = await ResolveSecretReferencesAsync(custom.Method, resolvedSecrets, warnings, cancellationToken),
                    BodyType = custom.BodyType,
                    Bodies = await ResolveSecretReferencesAsync(custom.Bodies, resolvedSecrets, warnings, cancellationToken),
                    Headers = await ResolveSecretReferencesAsync(custom.Headers, resolvedSecrets, warnings,
                        StringComparer.OrdinalIgnoreCase, cancellationToken),
                    Params = await ResolveSecretReferencesAsync(custom.Params, resolvedSecrets, warnings,
                        StringComparer.Ordinal, cancellationToken),
                    Source = custom.Source,
                    ExtractionExpression = await ResolveSecretReferencesAsync(custom.ExtractionExpression, resolvedSecrets, warnings, cancellationToken),
                    ApplyHeaderName = await ResolveSecretReferencesAsync(custom.ApplyHeaderName, resolvedSecrets, warnings, cancellationToken),
                    ApplyHeaderTemplate = await ResolveSecretReferencesAsync(custom.ApplyHeaderTemplate, resolvedSecrets, warnings, cancellationToken),
                    CachedValue = custom.CachedValue
                };
            default:
                return auth;
        }
    }

    private async Task<string> ResolveSecretReferencesAsync(
        string value,
        Dictionary<string, string> resolvedSecrets,
        List<string> warnings,
        CancellationToken cancellationToken)
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
            cancellationToken.ThrowIfCancellationRequested();
            string secretName = match.Groups["name"].Value.Trim();
            if (!resolvedSecrets.TryGetValue(secretName, out string? secretValue))
            {
                try
                {
                    StraumrSecret secret = await secretService.GetAsync(
                        secretName, updateLastAccessed: true, cancellationToken);
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
        IEqualityComparer<string> comparer,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> resolved = new Dictionary<string, string>(comparer);
        foreach (KeyValuePair<string, string> pair in source)
        {
            resolved[pair.Key] = await ResolveSecretReferencesAsync(
                pair.Value, resolvedSecrets, warnings, cancellationToken);
        }

        return resolved;
    }

    private async Task<Dictionary<BodyType, string>> ResolveSecretReferencesAsync(
        Dictionary<BodyType, string> source,
        Dictionary<string, string> resolvedSecrets,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        Dictionary<BodyType, string> resolved = new Dictionary<BodyType, string>();
        foreach (KeyValuePair<BodyType, string> pair in source)
        {
            resolved[pair.Key] = await ResolveSecretReferencesAsync(
                pair.Value, resolvedSecrets, warnings, cancellationToken);
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

    private async Task RemoveRequestAsync(
        StraumrWorkspaceEntry entry,
        StraumrWorkspace workspace,
        Guid id,
        CancellationToken cancellationToken)
    {
        string requestPath = RequestPath(id, entry);
        if (File.Exists(requestPath))
        {
            File.Delete(requestPath);
        }

        workspace.Requests.Remove(id);
        await PersistWorkspaceAsync(entry, workspace, cancellationToken);
    }

    private async Task PersistWorkspaceAsync(
        StraumrWorkspaceEntry entry,
        StraumrWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        await fileService.WriteStraumrModelAsync(
            entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);
    }

    private async Task<RequestLookup?> LookupRequestAsync(
        StraumrWorkspace workspace,
        string name,
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken)
    {
        foreach (Guid id in workspace.Requests)
        {
            try
            {
                StraumrRequest request = await ReadByIdAsync(
                    entry, id, updateLastAccessed: false, cancellationToken);
                if (string.Equals(request.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new RequestLookup(id, request);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<RequestLookup> RequireRequestAsync(StraumrWorkspace workspace, string name,
        string errorMessage, StraumrWorkspaceEntry entry, CancellationToken cancellationToken)
    {
        RequestLookup? lookup = await LookupRequestAsync(workspace, name, entry, cancellationToken);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }

    private readonly record struct RequestLookup(Guid Id, StraumrRequest Request);
}
