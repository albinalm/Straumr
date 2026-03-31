using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrWorkspaceService(
    IStraumrScope scope,
    IStraumrFileService fileService,
    IStraumrOptionsService optionsService) : IStraumrWorkspaceService
{
    private string WorkspacePath(string id) =>
        Path.Combine(optionsService.Options.DefaultWorkspacePath, id, id + ".straumr");

    public async Task Load(string name)
    {
        StraumrWorkspaceEntry? entry = optionsService.Options.Workspaces
            .SingleOrDefault(x => x.Name == name);

        if (entry is null)
        {
            throw new StraumrException($"No workspace found with the name: {name}",
                StraumrError.EntryNotFound);
        }

        if (!File.Exists(entry.Path))
        {
            throw new StraumrException("Failed to load workspace. Workspace file not found.",
                StraumrError.FileNotFound);
        }

        StraumrWorkspace workspace = await fileService.Read(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        entry.LastAccessed = DateTimeOffset.UtcNow;
        await optionsService.Save();
        scope.Workspace = workspace;
    }

    public async Task CreateAndOpen(StraumrWorkspace workspace)
    {
        string fullPath = WorkspacePath(workspace.Id);

        if (optionsService.Options.Workspaces.Any(x => x.Name == workspace.Name))
        {
            throw new StraumrException("A workspace with this name already exists in settings",
                StraumrError.FileConflict);
        }

        if (File.Exists(fullPath))
        {
            throw new StraumrException("A workspace already exists with the same name at this location",
                StraumrError.FileConflict);
        }

        await fileService.Write(fullPath, workspace, StraumrJsonContext.Default.StraumrWorkspace);

        optionsService.Options.Workspaces.Add(new StraumrWorkspaceEntry
        {
            Name = workspace.Name,
            Path = fullPath
        });
        await optionsService.Save();

        scope.Workspace = workspace;
    }

    public async Task Import(string path)
    {
        if (!File.Exists(path))
        {
            throw new StraumrException("Cannot import workspace. File doesn't exist", StraumrError.FileNotFound);
        }

        StraumrWorkspace workspace = await fileService.Read(path, StraumrJsonContext.Default.StraumrWorkspace);

        string destPath = WorkspacePath(workspace.Id);

        if (File.Exists(destPath))
        {
            throw new StraumrException("A workspace with this name already exists", StraumrError.FileConflict);
        }

        await fileService.Write(destPath, workspace, StraumrJsonContext.Default.StraumrWorkspace);

        //If we have an entry in collection but its missing physically from disk. We allow an overwrite
        optionsService.Options.Workspaces.RemoveAll(x => x.Name == workspace.Name);
        
        optionsService.Options.Workspaces.Add(new StraumrWorkspaceEntry
        {
            Name = workspace.Name,
            Path = destPath
        });
        await optionsService.Save();
    }

    public async Task Save()
    {
        if (scope.Workspace == null)
        {
            throw new StraumrException("No workspace loaded", StraumrError.InvalidScope);
        }

        string fullPath = WorkspacePath(scope.Workspace.Id);
        scope.Workspace.Modified = DateTimeOffset.UtcNow;
        await fileService.Write(fullPath, scope.Workspace, StraumrJsonContext.Default.StraumrWorkspace);
    }
}
