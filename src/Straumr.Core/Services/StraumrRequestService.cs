using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Straumr.Core.Configuration;
using Straumr.Core.Exceptions;
using Straumr.Core.Extensions;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrRequestService(
    IStraumrFileService fileService,
    IHttpClientFactory httpClientFactory,
    IStraumrAuthService authService,
    IStraumrSecretService secretService,
    IStraumrVariableService variableService) : IStraumrRequestService
{
    private static readonly Regex ReferencePattern = VariableHelpers.ReferencePattern;
    public async Task<IReadOnlyList<StraumrRequest>> ListAsync(
        StraumrWorkspaceEntry workspace,
        CancellationToken cancellationToken = default)
    {
        StraumrWorkspace workspaceModel = await LoadWorkspaceAsync(workspace, cancellationToken);
        List<StraumrRequest> requests = new();
        foreach (Guid id in workspaceModel.Requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                requests.Add(await ReadByIdAsync(
                    workspace, id, false, cancellationToken));
            }
            catch (StraumrException exception) when (
                exception.Reason is StraumrError.EntryNotFound or StraumrError.CorruptEntry)
            {
            }
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
        RequestLookupModel lookup = await RequireRequestAsync(
            workspaceModel, name, $"No request found with the name: {name}", workspace, cancellationToken);
        return await ResolveRequestAsync(lookup, workspace, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrRequest> CreateAsync(
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateName(request.Name);
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
        ValidateName(request.Name);
        string fullPath = RequestPath(request.Id, workspace);
        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        await EnsureNoNameConflictAsync(
            request.Name, workspace, request.Id, cancellationToken: cancellationToken);
        request.LastResponse = null;
        await fileService.WriteStraumrModelAsync(
            fullPath, request, StraumrJsonContext.Default.StraumrRequest, cancellationToken);
        return request;
    }

    public async Task StoreResponseAsync(
        StraumrWorkspaceEntry workspace,
        Guid id,
        StraumrStoredResponse? response,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = RequestPath(id, workspace);
        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        StraumrRequest request = await fileService.PeekStraumrModelAsync(
            fullPath, StraumrJsonContext.Default.StraumrRequest, cancellationToken);
        if (request.LastResponse is null && response is null)
        {
            return;
        }

        request.LastResponse = response;
        await fileService.WriteStraumrModelAsync(
            fullPath, request, StraumrJsonContext.Default.StraumrRequest, false, cancellationToken);
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
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        CancellationToken cancellationToken = default)
    {
        List<string> warnings = new();
        Dictionary<string, string> resolved = new(StringComparer.Ordinal);
        string resolvedUrl = await ResolveReferencesAsync(
            workspace, request.Uri, resolved, warnings, cancellationToken);
        return (resolvedUrl, warnings);
    }

    public async Task<StraumrResponse> SendAsync(
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        SendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        List<string> warnings = new();
        Dictionary<string, string> resolved = new(StringComparer.Ordinal);

        StraumrAuth? auth = request.AuthId.HasValue
            ? await authService.GetAsync(
                workspace, request.AuthId.Value, true, cancellationToken)
            : null;

        StraumrRequest resolvedRequest = await ResolveReferencesAsync(
            workspace, request, resolved, warnings, cancellationToken);

        StraumrAuthConfig? resolvedAuthConfig = auth is not null
            ? await ResolveAuthReferencesAsync(workspace, auth.Config, resolved, warnings, cancellationToken)
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

        using HttpClient client = httpClientFactory.CreateClient(StraumrHttpClientConstants.For(options));

        StraumrResponse response = await SendWithMetadataAsync(
            client, resolvedRequest, resolvedAuthConfig, cancellationToken);
        response.Warnings = warnings;

        if (ShouldRetryCustomAuth(auth, resolvedAuthConfig, response))
        {
            var custom = (CustomAuthConfig)resolvedAuthConfig!;
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

    public async Task<string> SendAuthAsync(
        StraumrWorkspaceEntry workspace,
        StraumrAuth auth,
        CancellationToken cancellationToken = default)
    {
        List<string> warnings = new();
        Dictionary<string, string> resolved = new(StringComparer.Ordinal);
        StraumrAuthConfig? config = await ResolveAuthReferencesAsync(
            workspace, auth.Config, resolved, warnings, cancellationToken);
        if (warnings.Count > 0)
        {
            throw new StraumrException(string.Join(" ", warnings), StraumrError.MissingEntry);
        }

        string value = config switch
        {
            OAuth2Config oauth => await FetchOAuthAsync(oauth, auth, cancellationToken),
            CustomAuthConfig custom => await FetchCustomAsync(custom, auth, cancellationToken),
            _ => throw new StraumrException("This auth has no request to send", StraumrError.InvalidEntry)
        };
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StraumrException("Auth response did not contain a usable value", StraumrError.InvalidEntry);
        }

        await authService.SaveAsync(workspace, auth, cancellationToken);
        return value;
    }

    private async Task<string> FetchOAuthAsync(OAuth2Config resolved, StraumrAuth auth, CancellationToken cancellationToken)
    {
        OAuth2Token token = await authService.FetchTokenAsync(resolved, cancellationToken);
        ((OAuth2Config)auth.Config).Token = token;
        return token.AccessToken;
    }

    private async Task<string> FetchCustomAsync(CustomAuthConfig resolved, StraumrAuth auth, CancellationToken cancellationToken)
    {
        string value = await authService.ExecuteCustomAuthAsync(resolved, cancellationToken);
        ((CustomAuthConfig)auth.Config).CachedValue = value;
        return value;
    }

    public string PathFor(StraumrWorkspaceEntry workspace, Guid id) => RequestPath(id, workspace);

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
                    entry, id, false, cancellationToken);
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
        return Path.Combine(directory!, id + ".jsonc");
    }

    private async Task<StraumrWorkspace> LoadWorkspaceAsync(
        StraumrWorkspaceEntry entry,
        CancellationToken cancellationToken = default) =>
        await fileService.PeekStraumrModelAsync(
            entry.Path, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);

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

    private static void ValidateName(string name)
    {
        if (name.Contains('"'))
        {
            throw new StraumrException(
                "Request names cannot contain double quotes",
                StraumrError.InvalidEntry);
        }
    }

    private async Task<StraumrRequest> ResolveRequestAsync(
        RequestLookupModel lookup,
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
        var networkRequest = request.ToHttpRequestMessage(auth);
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
        Dictionary<string, IEnumerable<string>> requestHeaders = new();
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

    private async Task<StraumrRequest> ResolveReferencesAsync(
        StraumrWorkspaceEntry workspace,
        StraumrRequest request,
        Dictionary<string, string> resolved,
        List<string> warnings,
        CancellationToken cancellationToken) =>
        new()
        {
            Id = request.Id,
            Name = request.Name,
            Modified = request.Modified,
            LastAccessed = request.LastAccessed,
            Uri = await ResolveReferencesAsync(workspace, request.Uri, resolved, warnings, cancellationToken),
            Method = request.Method,
            Params = await ResolveReferencesAsync(workspace, request.Params, resolved, warnings,
                StringComparer.Ordinal, cancellationToken),
            Headers = await ResolveReferencesAsync(workspace, request.Headers, resolved, warnings,
                StringComparer.OrdinalIgnoreCase, cancellationToken),
            BodyType = request.BodyType,
            Bodies = await ResolveReferencesAsync(workspace, request.Bodies, resolved, warnings, cancellationToken),
            AuthId = request.AuthId
        };

    private async Task<StraumrAuthConfig?> ResolveAuthReferencesAsync(
        StraumrWorkspaceEntry workspace,
        StraumrAuthConfig? auth,
        Dictionary<string, string> resolved,
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
                    Token = await ResolveReferencesAsync(workspace, bearer.Token, resolved, warnings, cancellationToken),
                    Prefix = await ResolveReferencesAsync(workspace, bearer.Prefix, resolved, warnings, cancellationToken)
                };
            case BasicAuthConfig basic:
                return new BasicAuthConfig
                {
                    Username = await ResolveReferencesAsync(workspace, basic.Username, resolved, warnings, cancellationToken),
                    Password = await ResolveReferencesAsync(workspace, basic.Password, resolved, warnings, cancellationToken)
                };
            case OAuth2Config oauth2:
                return new OAuth2Config
                {
                    GrantType = oauth2.GrantType,
                    TokenUrl = await ResolveReferencesAsync(workspace, oauth2.TokenUrl, resolved, warnings, cancellationToken),
                    ClientId = await ResolveReferencesAsync(workspace, oauth2.ClientId, resolved, warnings, cancellationToken),
                    ClientSecret = await ResolveReferencesAsync(workspace, oauth2.ClientSecret, resolved, warnings, cancellationToken),
                    Scope = await ResolveReferencesAsync(workspace, oauth2.Scope, resolved, warnings, cancellationToken),
                    AuthorizationUrl = await ResolveReferencesAsync(workspace, oauth2.AuthorizationUrl, resolved, warnings, cancellationToken),
                    RedirectUri = await ResolveReferencesAsync(workspace, oauth2.RedirectUri, resolved, warnings, cancellationToken),
                    UsePkce = oauth2.UsePkce,
                    CodeChallengeMethod = await ResolveReferencesAsync(workspace, oauth2.CodeChallengeMethod, resolved, warnings, cancellationToken),
                    Username = await ResolveReferencesAsync(workspace, oauth2.Username, resolved, warnings, cancellationToken),
                    Password = await ResolveReferencesAsync(workspace, oauth2.Password, resolved, warnings, cancellationToken),
                    Token = oauth2.Token
                };
            case CustomAuthConfig custom:
                return new CustomAuthConfig
                {
                    Url = await ResolveReferencesAsync(workspace, custom.Url, resolved, warnings, cancellationToken),
                    Method = await ResolveReferencesAsync(workspace, custom.Method, resolved, warnings, cancellationToken),
                    BodyType = custom.BodyType,
                    Bodies = await ResolveReferencesAsync(workspace, custom.Bodies, resolved, warnings, cancellationToken),
                    Headers = await ResolveReferencesAsync(workspace, custom.Headers, resolved, warnings,
                        StringComparer.OrdinalIgnoreCase, cancellationToken),
                    Params = await ResolveReferencesAsync(workspace, custom.Params, resolved, warnings,
                        StringComparer.Ordinal, cancellationToken),
                    Source = custom.Source,
                    ExtractionExpression = await ResolveReferencesAsync(workspace, custom.ExtractionExpression, resolved, warnings, cancellationToken),
                    ApplyHeaderName = await ResolveReferencesAsync(workspace, custom.ApplyHeaderName, resolved, warnings, cancellationToken),
                    ApplyHeaderTemplate = await ResolveReferencesAsync(workspace, custom.ApplyHeaderTemplate, resolved, warnings, cancellationToken, CustomAuthConfig.ValueName),
                    CachedValue = custom.CachedValue
                };
            default:
                return auth;
        }
    }

    private async Task<string> ResolveReferencesAsync(
        StraumrWorkspaceEntry workspace,
        string value,
        Dictionary<string, string> resolved,
        List<string> warnings,
        CancellationToken cancellationToken,
        string? reservedName = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        MatchCollection matches = ReferencePattern.Matches(value);
        if (matches.Count == 0)
        {
            return value;
        }

        StringBuilder builder = new(value.Length);
        int index = 0;
        foreach (Match match in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append(value, index, match.Index - index);
            builder.Append(await ResolveReferenceAsync(workspace, match, resolved, warnings, reservedName, cancellationToken));
            index = match.Index + match.Length;
        }

        return builder.Append(value, index, value.Length - index).ToString();
    }

    private async Task<string> ResolveReferenceAsync(
        StraumrWorkspaceEntry workspace,
        Match match,
        Dictionary<string, string> resolved,
        List<string> warnings,
        string? reservedName,
        CancellationToken cancellationToken)
    {
        string token = match.Groups["name"].Value.Trim();
        bool isSecret = VariableHelpers.IsSecretName(token);
        string name = isSecret ? VariableHelpers.StripSecretPrefix(token) : token;
        if (!isSecret && name.Equals(reservedName, StringComparison.OrdinalIgnoreCase))
        {
            return match.Value;
        }

        string key = isSecret ? $"Secret:{name}" : $"Variable:{name}";
        if (resolved.TryGetValue(key, out string? value))
        {
            return value;
        }

        try
        {
            value = isSecret
                ? (await secretService.GetAsync(name, true, cancellationToken)).Value
                : (await variableService.GetAsync(workspace, name, true, cancellationToken)).Value;
        }
        catch (StraumrException exception) when (exception.Reason == StraumrError.EntryNotFound)
        {
            string warning = $"{(isSecret ? "Secret" : "Variable")} '{name}' could not be resolved.";
            if (!warnings.Contains(warning, StringComparer.Ordinal))
            {
                warnings.Add(warning);
            }

            return match.Value;
        }

        resolved[key] = value;
        return value;
    }

    private async Task<Dictionary<string, string>> ResolveReferencesAsync(
        StraumrWorkspaceEntry workspace,
        Dictionary<string, string> source,
        Dictionary<string, string> resolved,
        List<string> warnings,
        IEqualityComparer<string> comparer,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> result = new(comparer);
        foreach (KeyValuePair<string, string> pair in source)
        {
            result[pair.Key] = await ResolveReferencesAsync(
                workspace, pair.Value, resolved, warnings, cancellationToken);
        }

        return result;
    }

    private async Task<Dictionary<BodyType, string>> ResolveReferencesAsync(
        StraumrWorkspaceEntry workspace,
        Dictionary<BodyType, string> source,
        Dictionary<string, string> resolved,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        Dictionary<BodyType, string> result = new();
        foreach (KeyValuePair<BodyType, string> pair in source)
        {
            result[pair.Key] = await ResolveReferencesAsync(
                workspace, pair.Value, resolved, warnings, cancellationToken);
        }

        return result;
    }


    private static bool ShouldRetryCustomAuth(
        StraumrAuth? auth, StraumrAuthConfig? resolvedAuthConfig, StraumrResponse response) =>
        auth is { AutoRenewAuth: true }
        && resolvedAuthConfig is CustomAuthConfig
        && response.StatusCode == HttpStatusCode.Unauthorized;

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

    private async Task<RequestLookupModel?> LookupRequestAsync(
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
                    entry, id, false, cancellationToken);
                if (string.Equals(request.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return new RequestLookupModel(id, request);
                }
            }
            catch (StraumrException) { }
        }

        return null;
    }

    private async Task<RequestLookupModel> RequireRequestAsync(StraumrWorkspace workspace, string name,
        string errorMessage, StraumrWorkspaceEntry entry, CancellationToken cancellationToken)
    {
        RequestLookupModel? lookup = await LookupRequestAsync(workspace, name, entry, cancellationToken);
        if (lookup.HasValue)
        {
            return lookup.Value;
        }

        throw new StraumrException(errorMessage, StraumrError.EntryNotFound);
    }
}
