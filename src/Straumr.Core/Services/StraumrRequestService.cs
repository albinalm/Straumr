using System.Net;
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
    IStraumrAuthService authService) : IStraumrRequestService
{
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

    public async Task<StraumrRequest> PeekByIdAsync(Guid id)
    {
        GetCurrentWorkspaceEntry();
        string fullPath = RequestPath(id);

        try
        {
            return await fileService.PeekStraumrModel(fullPath, StraumrJsonContext.Default.StraumrRequest);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid request", StraumrError.CorruptEntry, jex);
        }
    }

    public async Task CreateAsync(StraumrRequest request)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();

        string fullPath = RequestPath(request.Id);
        if (File.Exists(fullPath))
        {
            throw new StraumrException("Request already exists", StraumrError.EntryConflict);
        }

        await fileService.WriteStraumrModel(fullPath, request, StraumrJsonContext.Default.StraumrRequest);
        await AddRequestToWorkspace(entry, request.Id);
    }

    public async Task UpdateAsync(StraumrRequest request)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        string fullPath = RequestPath(request.Id);

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        await fileService.WriteStraumrModel(fullPath, request, StraumrJsonContext.Default.StraumrRequest);
        await StampWorkspaceAccessAsync(entry);
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
        var requestUpdated = false;

        switch (request.Auth)
        {
            case OAuth2Config oauth2 when request.AutoRenewAuth:
            {
                OAuth2Token token = await authService.EnsureTokenAsync(oauth2);
                oauth2.Token = token;
                requestUpdated = true;
                break;
            }
            case CustomAuthConfig { CachedValue: null } custom:
            {
                await authService.ExecuteCustomAuthAsync(custom);
                requestUpdated = true;
                break;
            }
        }

        if (requestUpdated)
        {
            await UpdateAsync(request);
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
            StraumrResponse response = await SendWithMetadataAsync(client, request);

            if (ShouldRetryCustomAuth(request, response))
            {
                var custom = (CustomAuthConfig)request.Auth!;
                await authService.ExecuteCustomAuthAsync(custom);
                await UpdateAsync(request);
                response = await SendWithMetadataAsync(client, request);
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
            await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        workspace.Requests.Add(requestId);
        await PersistWorkspaceAsync(entry, workspace);
    }

    private async Task<(StraumrWorkspaceEntry entry, StraumrWorkspace workspace)> LoadWorkspaceAsync()
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        StraumrWorkspace workspace =
            await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        return (entry, workspace);
    }

    private async Task<StraumrRequest> ResolveRequestAsync(RequestLookup lookup)
    {
        return lookup.Request ?? await GetByIdAsync(lookup.Id);
    }

    private static async Task<StraumrResponse> SendWithMetadataAsync(HttpClient client, StraumrRequest request)
    {
        var networkRequest = request.ToHttpRequestMessage();
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

    private static bool ShouldRetryCustomAuth(StraumrRequest request, StraumrResponse response)
    {
        return request.AutoRenewAuth
               && request.Auth is CustomAuthConfig
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

    private async Task StampWorkspaceAccessAsync(StraumrWorkspaceEntry entry)
    {
        await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
    }

    private async Task<RequestLookup?> LookupRequestAsync(StraumrWorkspace workspace, string identifier)
    {
        if (Guid.TryParse(identifier, out Guid requestId) && workspace.Requests.Contains(requestId))
        {
            return new RequestLookup(requestId, null);
        }

        foreach (Guid id in workspace.Requests)
        {
            StraumrRequest request = await PeekByIdAsync(id);
            if (request.Name == identifier)
            {
                return new RequestLookup(id, request);
            }
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