using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrRequestService(
    IStraumrFileService fileService,
    IStraumrOptionsService optionsService) : IStraumrRequestService
{
    private string RequestPath(Guid id)
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        string? directory = Path.GetDirectoryName(entry.Path);
        return Path.Combine(directory!, id + ".json");
    }

    public async Task<StraumrRequest> Get(Guid id)
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

    public async Task Create(StraumrRequest request)
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

    private StraumrWorkspaceEntry GetCurrentWorkspaceEntry()
    {
        return optionsService.Options.CurrentWorkspace
            ?? throw new StraumrException("No workspace loaded", StraumrError.MissingEntry);
    }

    private async Task AddRequestToWorkspace(StraumrWorkspaceEntry entry, Guid requestId)
    {
        StraumrWorkspace workspace = await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        workspace.Requests.Add(requestId);
        await fileService.WriteStraumrModel(entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace);
    }
}
