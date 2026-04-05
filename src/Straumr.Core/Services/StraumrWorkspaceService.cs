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
    public async Task Activate(string name)
    {
        StraumrWorkspaceEntry entry = await ResolveWorkspaceEntryAsync(name);
        optionsService.Options.CurrentWorkspace = entry;
        await optionsService.Save();
        await fileService.StampAccessAsync(entry.Path, StraumrJsonContext.Default.StraumrWorkspace);
    }

    public async Task Create(StraumrWorkspace workspace, string? outputDir = null)
    {
        string fullPath = WorkspacePath(workspace.Id, workspace.Name, outputDir);
        await EnsureNoConflict(workspace.Name, fullPath);

        await fileService.WriteStraumrModel(fullPath, workspace, StraumrJsonContext.Default.StraumrWorkspace);

        var entry = new StraumrWorkspaceEntry
        {
            Id = workspace.Id,
            Path = fullPath
        };

        optionsService.Options.Workspaces.Add(entry);
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
                Directory.Delete(extractPath, true);
            }
        }
    }

    public async Task Delete(string identifier)
    {
        try
        {
            StraumrWorkspaceEntry entry = await ResolveWorkspaceEntryAsync(identifier);

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

    public async Task Copy(string identifier, string newName, string? outputDir = null)
    {
        StraumrWorkspaceEntry sourceEntry = await ResolveWorkspaceEntryAsync(identifier);
        StraumrWorkspace sourceWorkspace = await PeekWorkspace(sourceEntry.Path);

        var newWorkspace = new StraumrWorkspace
        {
            Name = newName,
            Requests = sourceWorkspace.Requests,
            Auths = sourceWorkspace.Auths,
            Secrets = sourceWorkspace.Secrets
        };

        string newFullPath = WorkspacePath(newWorkspace.Id, newName, outputDir);
        await EnsureNoConflict(newName, newFullPath);

        string sourceDir = GetWorkspaceDirectory(sourceEntry);
        string destDir = Path.GetDirectoryName(newFullPath)!;
        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.EnumerateFiles(sourceDir))
        {
            if (Path.GetExtension(file).Equals(".straumr", StringComparison.OrdinalIgnoreCase))
                continue;
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
        }

        await fileService.WriteStraumrModel(newFullPath, newWorkspace, StraumrJsonContext.Default.StraumrWorkspace);

        optionsService.Options.Workspaces.Add(new StraumrWorkspaceEntry
        {
            Id = newWorkspace.Id,
            Path = newFullPath
        });
        await optionsService.Save();
    }

    public async Task<string> Export(string workspaceIdentifier, string outputDir)
    {
        EnsureValidOutputDirectory(outputDir);

        StraumrWorkspaceEntry entry = await ResolveWorkspaceEntryAsync(workspaceIdentifier);
        StraumrWorkspace workspace = await PeekWorkspace(entry.Path);
        string directoryName = GetWorkspaceDirectory(entry);

        string fullPath = Path.Combine(outputDir, workspace.Name.ToFileName() + ".straumrpak");

        await WriteExportArchive(fullPath, entry, directoryName, workspace.Name);
        return fullPath;
    }

    public async Task<string> PrepareEdit(string identifier)
    {
        StraumrWorkspaceEntry entry = await ResolveWorkspaceEntryAsync(identifier);
        string workspacePath = entry.Path;
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.Copy(workspacePath, tempPath, true);
        return tempPath;
    }

    public async Task ApplyEdit(string identifier, string tempPath)
    {
        StraumrWorkspaceEntry entry = await ResolveWorkspaceEntryAsync(identifier);
        File.Copy(tempPath, entry.Path, true);
    }

    public async Task<StraumrWorkspace> GetWorkspace(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                throw new StraumrException("Workspace not present on disk", StraumrError.EntryNotFound);
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

    public async Task<StraumrWorkspace> PeekWorkspace(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                throw new StraumrException("Workspace not found", StraumrError.EntryNotFound);
            }

            StraumrWorkspace? workspace =
                await fileService.PeekStraumrModel(path, StraumrJsonContext.Default.StraumrWorkspace);

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

    private string WorkspacePath(Guid id, string name, string? outputDir = null)
    {
        string workspaceRoot = GetWorkspaceRoot(outputDir);
        return Path.Combine(workspaceRoot, name.ToFileName(), id + ".straumr");
    }

    public async Task<string> GetWorkspaceName(string path)
    {
        StraumrWorkspace workspace = await PeekWorkspace(path);
        return workspace.Name;
    }

    public StraumrWorkspaceEntry GetWorkspaceEntry(Guid id)
    {
        foreach (StraumrWorkspaceEntry entry in
                 optionsService.Options.Workspaces.Where(entry => File.Exists(entry.Path)))
        {
            if (entry.Id == id)
            {
                return entry;
            }
        }

        throw new StraumrException($"No workspace found with the name: {id}",
            StraumrError.EntryNotFound);
    }

    private async Task<StraumrWorkspaceEntry> ResolveWorkspaceEntryAsync(string identifier)
    {
        if (Guid.TryParse(identifier, out Guid guid) && optionsService.Options.Workspaces.Any(x => x.Id == guid))
        {
            return GetWorkspaceEntry(guid);
        }

        foreach (StraumrWorkspaceEntry entry in optionsService.Options.Workspaces.Where(entry => File.Exists(entry.Path)))
        {
            try
            {
                StraumrWorkspace workspace = await PeekWorkspace(entry.Path);
                if (string.Equals(workspace.Name, identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            catch (StraumrException) { }
        }

        throw new StraumrException(
            $"A workspace was not found using the key {identifier}. Try again using the ID instead.",
            StraumrError.EntryNotFound);
    }

    private async Task EnsureNoConflict(string name, string fullPath)
    {
        foreach (StraumrWorkspaceEntry entry in optionsService.Options.Workspaces)
        {
            string workspaceName = await GetWorkspaceName(entry.Path);

            if (File.Exists(entry.Path) && string.Equals(workspaceName, name, StringComparison.OrdinalIgnoreCase))
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
        (Guid workspaceId, string workspaceName) = await ReadPakData(extractPath);

        string destinationPath = Path.Combine(GetWorkspaceRoot(), workspaceName.ToFileName());
        if (Directory.Exists(destinationPath))
        {
            throw new StraumrException("A workspace with this name already exists",
                StraumrError.EntryConflict);
        }

        Directory.Move(extractedWorkspacePath, destinationPath);

        optionsService.Options.Workspaces.RemoveAll(x => x.Id == workspaceId);
        optionsService.Options.Workspaces.Add(new StraumrWorkspaceEntry
        {
            Id = workspaceId,
            Path = WorkspacePath(workspaceId, workspaceName)
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

    private static async Task<(Guid id, string name)> ReadPakData(string extractPath)
    {
        string pakPath = Path.Combine(extractPath, ".pak");
        string[] pakData = await File.ReadAllLinesAsync(pakPath);
        return (Guid.Parse(pakData[0]), pakData[1]);
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

    private string GetWorkspaceRoot(string? outputDir = null)
    {
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            return outputDir;
        }

        return optionsService.Options.DefaultWorkspacePath
            ?? throw new StraumrException(
                "No default workspace path configured. Use '-o <path>' to specify an output directory or 'config workspace-path <path>'",
                StraumrError.MissingEntry);
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
