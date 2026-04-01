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
    IHttpClientFactory httpClientFactory) : IStraumrRequestService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient();

    private string RequestPath(Guid id)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        string? directory = Path.GetDirectoryName(entry.Path);
        return Path.Combine(directory!, id + ".json");
    }

    public async Task<StraumrRequest> GetAsync(string identifier)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        StraumrWorkspace workspace =
            await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);

        if (Guid.TryParse(identifier, out Guid requestId) && workspace.Requests.Contains(requestId))
        {
            return await GetByIdAsync(requestId);
        }

        foreach (Guid id in workspace.Requests)
        {
            StraumrRequest request = await GetByIdAsync(id);
            if (request.Name == identifier)
            {
                return request;
            }
        }

        throw new StraumrException($"No request found with the identifier: {identifier}",
            StraumrError.EntryNotFound);
    }

    public async Task DeleteAsync(string identifier)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        StraumrWorkspace workspace =
            await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);


        if (Guid.TryParse(identifier, out Guid requestId) && workspace.Requests.Contains(requestId))
        {
            RemoveRequestFile(requestId);
            workspace.Requests.Remove(requestId);
            await fileService.WriteStraumrModel(entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace);
            return;
        }

        StraumrRequest? request;
        foreach (Guid id in workspace.Requests)
        {
            request = await GetByIdAsync(id);
            if (request.Name != identifier) continue;

            RemoveRequestFile(id);
            workspace.Requests.Remove(id);
            await fileService.WriteStraumrModel(entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace);
            return;
        }

        throw new StraumrException("No request found", StraumrError.EntryNotFound);
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
        GetCurrentWorkspaceEntry();
        string fullPath = RequestPath(request.Id);

        if (!File.Exists(fullPath))
        {
            throw new StraumrException("Request not found", StraumrError.EntryNotFound);
        }

        await fileService.WriteStraumrModel(fullPath, request, StraumrJsonContext.Default.StraumrRequest);
    }

    public async Task<(Guid id, string tempPath)> PrepareEditAsync(string identifier)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        StraumrWorkspace workspace =
            await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);

        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

        if (Guid.TryParse(identifier, out Guid requestId) && workspace.Requests.Contains(requestId))
        {
            File.Copy(RequestPath(requestId), tempPath, overwrite: true);
            return (requestId, tempPath);
        }

        StraumrRequest? request;
        foreach (Guid id in workspace.Requests)
        {
            request = await GetByIdAsync(id);
            if (request.Name != identifier) continue;

            File.Copy(RequestPath(id), tempPath, overwrite: true);
            return (id, tempPath);
        }

        throw new StraumrException("No request found", StraumrError.EntryNotFound);
    }

    public void ApplyEdit(Guid requestId, string tempPath)
    {
        File.Copy(tempPath, RequestPath(requestId), overwrite: true);
    }

    public async Task<StraumrResponse> SendAsync(StraumrRequest request)
    {
        var networkRequest = request.ToHttpRequestMessage();
        return await _client.SendAsync(networkRequest).WithMetrics();
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
        await fileService.WriteStraumrModel(entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace);
    }
}
