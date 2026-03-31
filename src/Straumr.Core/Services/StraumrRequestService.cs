using System.Text.Json;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrRequestService(
    IStraumrScope scope,
    IStraumrFileService fileService,
    IStraumrOptionsService optionsService) : IStraumrRequestService
{
    private string RequestPath(string id) =>
        Path.Combine(optionsService.Options.DefaultWorkspacePath, scope.Workspace!.Id, id + ".json");

    public async Task<StraumrRequest> Get(string id)
    {
        if (scope.Workspace == null)
        {
            throw new StraumrException("No workspace loaded", StraumrError.InvalidScope);
        }

        string fullPath = RequestPath(id);

        try
        {
            return await fileService.Read(fullPath, StraumrJsonContext.Default.StraumrRequest);
        }
        catch (JsonException jex)
        {
            throw new StraumrException("Invalid request", StraumrError.FileCorrupt, jex);
        }
    }

    public async Task Create(StraumrRequest request)
    {
        if (scope.Workspace == null)
        {
            throw new StraumrException("No workspace loaded", StraumrError.InvalidScope);
        }

        string fullPath = RequestPath(request.Id);
        if (File.Exists(fullPath))
        {
            throw new StraumrException("Request already exists", StraumrError.FileConflict);
        }

        await fileService.Write(fullPath, request, StraumrJsonContext.Default.StraumrRequest);
        scope.Workspace.Requests.Add(request.Id);
    }
}
