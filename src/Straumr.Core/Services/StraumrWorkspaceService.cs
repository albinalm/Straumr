using System.IO.Compression;
using System.Text;
using Straumr.Core.Configuration;
using Straumr.Core.Enums;
using Straumr.Core.Exceptions;
using Straumr.Core.Extensions;
using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrWorkspaceService(IStraumrFileService fileService, IStraumrOptionsService optionsService)
    : IStraumrWorkspaceService
{
    private string WorkspacePath(string id) =>
        Path.Combine(optionsService.Options.DefaultWorkspacePath, id, id + ".straumr");

    public async Task Load(string name)
    {
        StraumrWorkspaceEntry entry = GetWorkspaceEntry(name);
        await fileService.Read(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);

        entry.LastAccessed = DateTimeOffset.UtcNow;
        optionsService.Options.CurrentWorkspace = entry;
        await optionsService.Save();
    }

    public async Task CreateAndOpen(StraumrWorkspace workspace)
    {
        string fullPath = WorkspacePath(workspace.Id);
        EnsureNoConflict(workspace.Name, fullPath);

        await fileService.Write(fullPath, workspace, StraumrJsonContext.Default.StraumrWorkspace);

        var entry = new StraumrWorkspaceEntry
        {
            Name = workspace.Name,
            Path = fullPath
        };

        optionsService.Options.Workspaces.Add(entry);
        optionsService.Options.CurrentWorkspace = entry;
        await optionsService.Save();
    }

    public async Task Import(string path)
    {
        if (!File.Exists(path))
        {
            throw new StraumrException("Cannot import workspace. File doesn't exist",
                StraumrError.EntryNotFound);
        }

        string extractPath = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
        Directory.CreateDirectory(extractPath);

        try
        {
            await ZipFile.ExtractToDirectoryAsync(path, extractPath);
            await ImportExtractedWorkspace(extractPath);
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
            DeleteOrphanedWorkspace(name);
            return;
        }

        DeleteDirectory(Path.GetDirectoryName(entry.Path));
        optionsService.Options.Workspaces.Remove(entry);
        await optionsService.Save();
    }

    public async Task<string> Export(string workspaceName, string outputDir)
    {
        EnsureValidOutputDirectory(outputDir);

        StraumrWorkspaceEntry entry = GetWorkspaceEntry(workspaceName);
        string directoryName = GetWorkspaceDirectory(entry);

        string pakName = entry.Name.ToStraumrId();
        string fullPath = Path.Combine(outputDir, pakName + ".straumrpak");

        await WriteExportArchive(fullPath, entry, directoryName);
        return fullPath;
    }

    public async Task Save()
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        StraumrWorkspace workspace = await fileService.Read(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        workspace.Modified = DateTimeOffset.UtcNow;
        await fileService.Write(entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace);
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

    private StraumrWorkspaceEntry GetCurrentWorkspaceEntry()
    {
        return optionsService.Options.CurrentWorkspace
               ?? throw new StraumrException("No workspace loaded", StraumrError.InvalidScope);
    }

    private void EnsureNoConflict(string name, string fullPath)
    {
        if (optionsService.Options.Workspaces.Any(x => x.Name == name))
        {
            throw new StraumrException("A workspace with this name already exists in settings",
                StraumrError.EntryConflict);
        }

        if (File.Exists(fullPath))
        {
            throw new StraumrException("A workspace already exists with the same name at this location",
                StraumrError.EntryConflict);
        }
    }

    private async Task ImportExtractedWorkspace(string extractPath)
    {
        string extractedWorkspacePath = ValidateExtractedArchive(extractPath);
        (string workspaceId, string workspaceName, DateTimeOffset lastAccessed) = await ReadPakFile(extractPath);

        string destinationPath = Path.Combine(optionsService.Options.DefaultWorkspacePath, workspaceId);
        if (Directory.Exists(destinationPath))
        {
            throw new StraumrException("A workspace with this name already exists",
                StraumrError.EntryConflict);
        }

        Directory.Move(extractedWorkspacePath, destinationPath);

        optionsService.Options.Workspaces.RemoveAll(x => x.Name == workspaceName);
        optionsService.Options.Workspaces.Add(new StraumrWorkspaceEntry
        {
            Name = workspaceName,
            Path = WorkspacePath(workspaceId),
            LastAccessed = lastAccessed
        });

        await optionsService.Save();
    }

    private static string ValidateExtractedArchive(string extractPath)
    {
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

        return directories[0];
    }

    private static async Task<(string Id, string Name, DateTimeOffset LastAccessed)> ReadPakFile(string extractPath)
    {
        string pakPath = Path.Combine(extractPath, ".pak");
        string[] pakData = await File.ReadAllLinesAsync(pakPath);
        return (pakData[0], pakData[1], DateTimeOffset.Parse(pakData[2]));
    }

    private void DeleteOrphanedWorkspace(string name)
    {
        string id = name.ToStraumrId();
        string path = Path.Combine(optionsService.Options.DefaultWorkspacePath, id);
        DeleteDirectory(path);
    }

    private static void DeleteDirectory(string? path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private static void EnsureValidOutputDirectory(string outputDir)
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
    }

    private static string GetWorkspaceDirectory(StraumrWorkspaceEntry entry)
    {
        string? directoryName = Path.GetDirectoryName(entry.Path);
        if (string.IsNullOrEmpty(directoryName))
        {
            throw new StraumrException(
                "Failed to get directory name from workspace entry.",
                StraumrError.CorruptEntry);
        }

        return directoryName;
    }

    private static async Task WriteExportArchive(
        string fullPath, StraumrWorkspaceEntry entry, string directoryName)
    {
        await using FileStream zipStream = new(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        await using ZipArchive archive = new(zipStream, ZipArchiveMode.Create);

        await WritePakEntry(archive, entry);
        await WriteWorkspaceFiles(archive, directoryName);
    }

    private static async Task WritePakEntry(ZipArchive archive, StraumrWorkspaceEntry entry)
    {
        ZipArchiveEntry pakEntry = archive.CreateEntry(".pak", CompressionLevel.SmallestSize);
        await using Stream pakStream = await pakEntry.OpenAsync();
        await using StreamWriter writer = new(pakStream, Encoding.UTF8);

        await writer.WriteLineAsync(entry.Name.ToStraumrId());
        await writer.WriteLineAsync(entry.Name);
        await writer.WriteLineAsync(entry.LastAccessed.ToString());
    }

    private static async Task WriteWorkspaceFiles(ZipArchive archive, string directoryName)
    {
        string baseFolderName =
            Path.GetFileName(directoryName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        foreach (string filePath in Directory.EnumerateFiles(directoryName, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFileName(filePath), ".pak", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(directoryName, filePath);
            string entryPath = Path.Combine(baseFolderName, relativePath).Replace('\\', '/');
            await archive.CreateEntryFromFileAsync(filePath, entryPath, CompressionLevel.SmallestSize);
        }
    }
}