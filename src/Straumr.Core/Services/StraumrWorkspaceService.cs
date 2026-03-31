using System.IO.Compression;
using System.Text;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Extensions;
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
        StraumrWorkspaceEntry entry = GetWorkspaceEntry(name);

        StraumrWorkspace workspace = await fileService.Read(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        entry.LastAccessed = DateTimeOffset.UtcNow;
        optionsService.Options.CurrentWorkspace = entry;
        await optionsService.Save();
        scope.Workspace = workspace;
    }

    private StraumrWorkspaceEntry GetWorkspaceEntry(string name)
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
                StraumrError.EntryNotFound);
        }

        return entry;
    }

    public async Task CreateAndOpen(StraumrWorkspace workspace)
    {
        string fullPath = WorkspacePath(workspace.Id);

        if (optionsService.Options.Workspaces.Any(x => x.Name == workspace.Name))
        {
            throw new StraumrException("A workspace with this name already exists in settings",
                StraumrError.EntryConflict);
        }

        if (File.Exists(fullPath))
        {
            throw new StraumrException("A workspace already exists with the same name at this location",
                StraumrError.EntryConflict);
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
            throw new StraumrException("Cannot import workspace. File doesn't exist", StraumrError.EntryNotFound);
        }

        string extractPath = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
        Directory.CreateDirectory(extractPath);

        try
        {
            await ZipFile.ExtractToDirectoryAsync(path, extractPath);

            string pakPath = Path.Combine(extractPath, ".pak");
            if (!File.Exists(pakPath))
            {
                throw new StraumrException("Cannot import workspace. Archive does not contain a .pak file",
                    StraumrError.EntryNotFound);
            }

            string[] directories = Directory.GetDirectories(extractPath);
            if (directories.Length == 0)
            {
                throw new StraumrException("Cannot import workspace. Archive does not contain a workspace",
                    StraumrError.CorruptEntry);
            }

            if (directories.Length > 1)
            {
                throw new StraumrException(
                    "Cannot import workspace. Archive is ambiguous and contains multiple folders",
                    StraumrError.InvalidEntry);
            }
            
            string extractedWorkspacePath =  directories[0];
            
            string[] pakData = await File.ReadAllLinesAsync(pakPath);
            string workspaceId = pakData[0];
            string workspaceName = pakData[1];
            DateTimeOffset lastAccessed = DateTimeOffset.Parse(pakData[2]);
            
            string workspaceDestinationPath = Path.Combine(optionsService.Options.DefaultWorkspacePath, workspaceId);
            if (Directory.Exists(workspaceDestinationPath))
            {
                throw new StraumrException(
                    "A workspace with this name already exists",
                    StraumrError.EntryConflict);
            }
            
            Directory.Move(extractedWorkspacePath, workspaceDestinationPath);
            
            // If we have an entry in collection but it's missing physically from disk, we allow an overwrite.
            optionsService.Options.Workspaces.RemoveAll(x => x.Name == workspaceName);

            optionsService.Options.Workspaces.Add(new StraumrWorkspaceEntry
            {
                Name = workspaceName,
                Path = WorkspacePath(workspaceId),
                LastAccessed = lastAccessed
            });

            await optionsService.Save();
        }
        finally
        {
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, recursive: true);
            }
        }
    }

    public async Task Delete(string name)
    {
        StraumrWorkspaceEntry? entry = optionsService.Options.Workspaces
            .SingleOrDefault(x => x.Name == name);

        if (entry is null)
        {
            string id = name.ToStraumrId();
            string path = Path.Combine(optionsService.Options.DefaultWorkspacePath, id);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        else
        {
            string? directory = Path.GetDirectoryName(entry.Path);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            optionsService.Options.Workspaces.Remove(entry);
            await optionsService.Save();
        }
    }

    public async Task<string> Export(string workspaceName, string outputDir)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(outputDir)))
        {
            throw new StraumrException("Output must be a directory", StraumrError.InvalidPath);
        }

        if (File.Exists(outputDir))
        {
            throw new StraumrException("Output must be a directory", StraumrError.InvalidPath);
        }

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        StraumrWorkspaceEntry entry = GetWorkspaceEntry(workspaceName);

        string pakName = entry.Name.ToStraumrId();
        string fullPath = Path.Combine(outputDir, pakName + ".straumrpak");

        string? directoryName = Path.GetDirectoryName(entry.Path);
        if (string.IsNullOrEmpty(directoryName))
        {
            throw new StraumrException(
                "Failed to get directory name from workspace entry.",
                StraumrError.CorruptEntry);
        }

        await using FileStream zipStream = new(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        await using ZipArchive archive = new(zipStream, ZipArchiveMode.Create);

        ZipArchiveEntry pakEntry = archive.CreateEntry(".pak", CompressionLevel.SmallestSize);
        await using (Stream pakStream = await pakEntry.OpenAsync())
        await using (StreamWriter writer = new(pakStream, Encoding.UTF8))
        {
            await writer.WriteLineAsync(entry.Name.ToStraumrId());
            await writer.WriteLineAsync(entry.Name);
            await writer.WriteLineAsync(entry.LastAccessed.ToString());
        }

        string baseFolderName =
            Path.GetFileName(directoryName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        foreach (string filePath in Directory.EnumerateFiles(directoryName, "*", SearchOption.AllDirectories))
        {
            // Don't include a physical .pak file from disk if one exists there
            if (string.Equals(Path.GetFileName(filePath), ".pak", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(directoryName, filePath);
            string entryPath = Path.Combine(baseFolderName, relativePath).Replace('\\', '/');

            await archive.CreateEntryFromFileAsync(filePath, entryPath, CompressionLevel.SmallestSize);
        }

        return fullPath;
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