using System.IO.Compression;
using System.Text;
using System.Text.Json;
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
    private string WorkspacePath(Guid id) =>
        Path.Combine(optionsService.Options.DefaultWorkspacePath, id.ToString(), id + ".straumr");

    public async Task Activate(string name)
    {
        StraumrWorkspaceEntry entry = await GetWorkspaceEntry(name);
        StraumrWorkspace workspace = await GetWorkspace(entry.Path);
        optionsService.Options.CurrentWorkspace = entry;
        await optionsService.Save();
    }

    public async Task CreateAndOpen(StraumrWorkspace workspace)
    {
        string fullPath = WorkspacePath(workspace.Id);
        await EnsureNoConflict(workspace.Name, fullPath);

        await fileService.WriteStraumrModel(fullPath, workspace, StraumrJsonContext.Default.StraumrWorkspace);

        var entry = new StraumrWorkspaceEntry
        {
            Id = workspace.Id,
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

    public async Task Delete(string identifier)
    {
        try
        {
            StraumrWorkspaceEntry entry = await GetWorkspaceEntry(identifier);

            DeleteDirectory(Path.GetDirectoryName(entry.Path));
            optionsService.Options.Workspaces.Remove(entry);
            await optionsService.Save();
        }
        catch (StraumrException ex) when (ex.Reason is StraumrError.CorruptEntry or StraumrError.EntryNotFound)
        {
            throw new StraumrException(
                $"A workspace was not found using the key {identifier}. Try again using the ID instead.",
                StraumrError.EntryNotFound);
        }
    }

    public async Task<string> Export(string workspaceName, string outputDir)
    {
        EnsureValidOutputDirectory(outputDir);

        StraumrWorkspaceEntry entry = await GetWorkspaceEntry(workspaceName);
        string directoryName = GetWorkspaceDirectory(entry);
        string name = await GetWorkspaceName(entry.Path);

        string fullPath = Path.Combine(outputDir, name.ToFileName() + ".straumrpak");

        await WriteExportArchive(fullPath, entry, directoryName, name);
        return fullPath;
    }

    public async Task Save()
    {
        StraumrWorkspaceEntry entry = GetCurrentWorkspaceEntry();
        StraumrWorkspace workspace =
            await fileService.ReadStraumrModel(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
        workspace.Modified = DateTimeOffset.UtcNow;
        await fileService.WriteStraumrModel(entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace);
    }

    public async Task<string> PrepareEdit(string name)
    {
        StraumrWorkspaceEntry entry = await GetWorkspaceEntry(name);
        string workspacePath = entry.Path;
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.Copy(workspacePath, tempPath, overwrite: true);
        return tempPath;
    }

    public async Task ApplyEdit(string name, string tempPath)
    {
        StraumrWorkspaceEntry entry = await GetWorkspaceEntry(name);
        File.Copy(tempPath, entry.Path, overwrite: true);
    }

    public async Task<string> GetWorkspaceName(string path)
    {
        StraumrWorkspace workspace = await GetWorkspace(path);
        return workspace.Name;
    }

    public async Task<StraumrWorkspace> GetWorkspace(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                throw new StraumrException("Workspace not found", StraumrError.EntryNotFound);
            }

            StraumrWorkspace? workspace =
                await fileService.ReadStraumrModel(path, StraumrJsonContext.Default.StraumrWorkspace);

            if (workspace is null)
            {
                throw new StraumrException("Failed to read workspace", StraumrError.CorruptEntry);
            }

            return workspace;
        }
        catch (JsonException ex)
        {
            throw new StraumrException("Workspace is corrupt", StraumrError.CorruptEntry, ex);
        }
    }

    private async Task<StraumrWorkspaceEntry> GetWorkspaceEntry(string identifier)
    {
        foreach (StraumrWorkspaceEntry entry in
                 optionsService.Options.Workspaces.Where(entry => File.Exists(entry.Path)))
        {
            if (entry.Id.ToString() == identifier)
            {
                return entry;
            }

            StraumrWorkspace workspace = await GetWorkspace(entry.Path);
            if (workspace.Name == identifier || workspace.Id.ToString() == identifier)
            {
                return entry;
            }
        }

        throw new StraumrException($"No workspace found with the name: {identifier}",
            StraumrError.EntryNotFound);
    }

    private StraumrWorkspaceEntry GetCurrentWorkspaceEntry()
    {
        return optionsService.Options.CurrentWorkspace
               ?? throw new StraumrException("No workspace loaded", StraumrError.InvalidScope);
    }

    private async Task EnsureNoConflict(string name, string fullPath)
    {
        foreach (StraumrWorkspaceEntry entry in optionsService.Options.Workspaces)
        {
            string workspaceName = await GetWorkspaceName(entry.Path);

            if (File.Exists(entry.Path) && workspaceName == name)
            {
                throw new StraumrException("A workspace with this name already exists",
                    StraumrError.EntryConflict);
            }
        }

        if (File.Exists(fullPath))
        {
            throw new StraumrException("A workspace already exists at this location",
                StraumrError.EntryConflict);
        }
    }

    private async Task ImportExtractedWorkspace(string extractPath)
    {
        string extractedWorkspacePath = ValidateExtractedArchive(extractPath);
        Guid workspaceId = await ReadPakFile(extractPath);

        string destinationPath = Path.Combine(optionsService.Options.DefaultWorkspacePath, workspaceId.ToString());
        if (Directory.Exists(destinationPath))
        {
            throw new StraumrException("A workspace with this id already exists",
                StraumrError.EntryConflict);
        }

        Directory.Move(extractedWorkspacePath, destinationPath);

        optionsService.Options.Workspaces.RemoveAll(x => x.Id == workspaceId);
        optionsService.Options.Workspaces.Add(new StraumrWorkspaceEntry
        {
            Id = workspaceId,
            Path = WorkspacePath(workspaceId)
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

    private static async Task<Guid> ReadPakFile(string extractPath)
    {
        string pakPath = Path.Combine(extractPath, ".pak");
        string[] pakData = await File.ReadAllLinesAsync(pakPath);
        return Guid.Parse(pakData[0]);
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
        string fullPath, StraumrWorkspaceEntry entry, string directoryName, string name)
    {
        await using FileStream zipStream = new(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        await using ZipArchive archive = new(zipStream, ZipArchiveMode.Create);

        await WritePakEntry(archive, entry, name);
        await WriteWorkspaceFiles(archive, directoryName);
    }

    private static async Task WritePakEntry(ZipArchive archive, StraumrWorkspaceEntry entry, string name)
    {
        ZipArchiveEntry pakEntry = archive.CreateEntry(".pak", CompressionLevel.SmallestSize);
        await using Stream pakStream = await pakEntry.OpenAsync();
        await using StreamWriter writer = new(pakStream, Encoding.UTF8);

        await writer.WriteLineAsync(entry.Id.ToString());
        await writer.WriteLineAsync(name);
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