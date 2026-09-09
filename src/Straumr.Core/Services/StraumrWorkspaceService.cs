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
    public async Task<IReadOnlyList<StraumrWorkspace>> ListAsync(CancellationToken cancellationToken = default)
    {
        List<StraumrWorkspace> workspaces = new List<StraumrWorkspace>();
        foreach (StraumrWorkspaceEntry entry in optionsService.Options.Workspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                workspaces.Add(await ReadWorkspaceAsync(
                    entry.Path, updateLastAccessed: false, cancellationToken));
            }
            catch (StraumrException exception) when (
                exception.Reason is StraumrError.EntryNotFound or StraumrError.CorruptEntry)
            {
            }
        }

        return workspaces;
    }

    public async Task<StraumrWorkspace> GetAsync(
        Guid id,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspaceEntry entry = GetEntry(id);
        return await ReadWorkspaceAsync(entry.Path, updateLastAccessed, cancellationToken);
    }

    public async Task<StraumrWorkspace> GetAsync(
        string name,
        bool updateLastAccessed = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        (StraumrWorkspaceEntry entry, StraumrWorkspace workspace) =
            await ResolveWorkspaceAsync(name, cancellationToken);
        if (updateLastAccessed)
        {
            await fileService.StampAccessAsync(
                entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);
        }

        return workspace;
    }

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspaceEntry entry = GetEntry(id);
        optionsService.Options.CurrentWorkspace = entry;
        await optionsService.SaveAsync(cancellationToken);
        await fileService.StampAccessAsync(
            entry.Path, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);
    }

    public async Task<StraumrWorkspace> CreateAsync(
        StraumrWorkspace workspace,
        string? outputDir = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = WorkspacePath(workspace.Id, workspace.Name, outputDir);
        await EnsureNoConflictAsync(workspace.Name, fullPath, cancellationToken: cancellationToken);

        await fileService.WriteStraumrModelAsync(
            fullPath, workspace, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);

        var entry = new StraumrWorkspaceEntry
        {
            Id = workspace.Id,
            Path = fullPath
        };

        optionsService.Options.Workspaces.Add(entry);
        await optionsService.SaveAsync(cancellationToken);
        return workspace;
    }

    public async Task<StraumrWorkspace> SaveAsync(
        StraumrWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspaceEntry entry = GetEntry(workspace.Id);
        await EnsureNoConflictAsync(workspace.Name, entry.Path, workspace.Id, cancellationToken);
        await fileService.WriteStraumrModelAsync(
            entry.Path, workspace, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);
        return workspace;
    }

    public async Task<StraumrWorkspaceEntry> ImportAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path))
        {
            throw new StraumrException("Cannot import workspace. File doesn't exist",
                StraumrError.EntryNotFound);
        }

        string extractPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(extractPath);

        try
        {
            await ZipFile.ExtractToDirectoryAsync(path, extractPath, cancellationToken);
            return await ImportExtractedWorkspaceAsync(extractPath, cancellationToken);
        }
        finally
        {
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspaceEntry entry = GetEntry(id);

        string? path = Path.GetDirectoryName(entry.Path);
        if (string.IsNullOrEmpty(path))
        {
            throw new StraumrException("Failed to resolve workspace path", StraumrError.InvalidEntry);
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        optionsService.Options.Workspaces.Remove(entry);
        if (optionsService.Options.CurrentWorkspace != null && optionsService.Options.CurrentWorkspace.Id == entry.Id)
        {
            optionsService.Options.CurrentWorkspace = null;
        }

        await optionsService.SaveAsync(cancellationToken);
    }

    public async Task<StraumrWorkspaceEntry> CopyAsync(
        Guid id,
        string newName,
        string? outputDir = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StraumrWorkspaceEntry sourceEntry = GetEntry(id);
        StraumrWorkspace sourceWorkspace = await ReadWorkspaceAsync(
            sourceEntry.Path, updateLastAccessed: false, cancellationToken);

        var newWorkspace = new StraumrWorkspace
        {
            Name = newName,
            Requests = new HashSet<Guid>(sourceWorkspace.Requests),
            Auths = new HashSet<Guid>(sourceWorkspace.Auths),
            Secrets = new HashSet<Guid>(sourceWorkspace.Secrets)
        };

        string newFullPath = WorkspacePath(newWorkspace.Id, newName, outputDir);
        await EnsureNoConflictAsync(newName, newFullPath, cancellationToken: cancellationToken);

        string sourceDir = GetWorkspaceDirectory(sourceEntry);
        string destDir = Path.GetDirectoryName(newFullPath)!;
        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.EnumerateFiles(sourceDir))
        {
            if (Path.GetExtension(file).Equals(".straumr", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
        }

        await fileService.WriteStraumrModelAsync(
            newFullPath, newWorkspace, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);

        var newEntry = new StraumrWorkspaceEntry { Id = newWorkspace.Id, Path = newFullPath };
        optionsService.Options.Workspaces.Add(newEntry);
        await optionsService.SaveAsync(cancellationToken);
        return newEntry;
    }

    public async Task<string> ExportAsync(
        Guid id,
        string outputDir,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureValidOutputDirectory(outputDir);

        StraumrWorkspaceEntry entry = GetEntry(id);
        StraumrWorkspace workspace = await ReadWorkspaceAsync(
            entry.Path, updateLastAccessed: false, cancellationToken);
        string directoryName = GetWorkspaceDirectory(entry);

        string fullPath = Path.Combine(outputDir, workspace.Name.ToFileName() + ".straumrpak");

        await WriteExportArchive(fullPath, entry, directoryName, workspace.Name, cancellationToken);
        return fullPath;
    }

    private async Task<StraumrWorkspace> ReadWorkspaceAsync(
        string path,
        bool updateLastAccessed,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(path))
            {
                throw new StraumrException("Workspace not found", StraumrError.EntryNotFound);
            }

            StraumrWorkspace? workspace = updateLastAccessed
                ? await fileService.ReadStraumrModelAsync(
                    path, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken)
                : await fileService.PeekStraumrModelAsync(
                    path, StraumrJsonContext.Default.StraumrWorkspace, cancellationToken);

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

    private async Task<string> GetWorkspaceNameAsync(
        string path,
        CancellationToken cancellationToken)
    {
        StraumrWorkspace workspace = await ReadWorkspaceAsync(
            path, updateLastAccessed: false, cancellationToken);
        return workspace.Name;
    }

    public StraumrWorkspaceEntry GetEntry(Guid id)
    {
        foreach (StraumrWorkspaceEntry entry in
                 optionsService.Options.Workspaces.Where(entry => File.Exists(entry.Path)))
        {
            if (entry.Id == id)
            {
                return entry;
            }
        }

        throw new StraumrException($"No workspace found with the ID: {id}",
            StraumrError.EntryNotFound);
    }

    private async Task<(StraumrWorkspaceEntry Entry, StraumrWorkspace Workspace)> ResolveWorkspaceAsync(
        string name,
        CancellationToken cancellationToken)
    {
        foreach (StraumrWorkspaceEntry entry in
                 optionsService.Options.Workspaces.Where(entry => File.Exists(entry.Path)))
        {
            try
            {
                StraumrWorkspace workspace = await ReadWorkspaceAsync(
                    entry.Path, updateLastAccessed: false, cancellationToken);
                if (string.Equals(workspace.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return (entry, workspace);
                }
            }
            catch (StraumrException) { }
        }

        throw new StraumrException(
            $"A workspace was not found with the name {name}.",
            StraumrError.EntryNotFound);
    }

    private async Task EnsureNoConflictAsync(
        string name,
        string fullPath,
        Guid excludeId = default,
        CancellationToken cancellationToken = default)
    {
        foreach (StraumrWorkspaceEntry entry in optionsService.Options.Workspaces)
        {
            if (entry.Id == excludeId || !File.Exists(entry.Path))
            {
                continue;
            }

            string workspaceName = await GetWorkspaceNameAsync(entry.Path, cancellationToken);

            if (string.Equals(workspaceName, name, StringComparison.OrdinalIgnoreCase))
            {
                throw new StraumrException("A workspace with this name already exists",
                    StraumrError.EntryConflict);
            }
        }

        if (excludeId == default && File.Exists(fullPath))
        {
            throw new StraumrException("A workspace already exists at this location",
                StraumrError.EntryConflict);
        }
    }

    private async Task<StraumrWorkspaceEntry> ImportExtractedWorkspaceAsync(
        string extractPath,
        CancellationToken cancellationToken)
    {
        string extractedWorkspacePath = ValidateExtractedArchive(extractPath);
        (Guid workspaceId, string workspaceName) = await ReadPakDataAsync(extractPath, cancellationToken);

        string destinationPath = Path.Combine(GetWorkspaceRoot(), workspaceName.ToFileName());
        if (Directory.Exists(destinationPath))
        {
            throw new StraumrException("A workspace with this name already exists",
                StraumrError.EntryConflict);
        }

        Directory.Move(extractedWorkspacePath, destinationPath);

        StraumrWorkspaceEntry entry = new StraumrWorkspaceEntry
        {
            Id = workspaceId,
            Path = WorkspacePath(workspaceId, workspaceName)
        };

        optionsService.Options.Workspaces.RemoveAll(x => x.Id == workspaceId);
        optionsService.Options.Workspaces.Add(entry);

        await optionsService.SaveAsync(cancellationToken);
        return entry;
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

    private static async Task<(Guid id, string name)> ReadPakDataAsync(
        string extractPath,
        CancellationToken cancellationToken)
    {
        string pakPath = Path.Combine(extractPath, ".pak");
        string[] pakData = await File.ReadAllLinesAsync(pakPath, cancellationToken);
        return (Guid.Parse(pakData[0]), pakData[1]);
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
        string fullPath,
        StraumrWorkspaceEntry entry,
        string directoryName,
        string name,
        CancellationToken cancellationToken)
    {
        await using FileStream zipStream = new(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        await using ZipArchive archive = new(zipStream, ZipArchiveMode.Create);

        await WritePakEntry(archive, entry, name, cancellationToken);
        await WriteWorkspaceFiles(archive, directoryName, cancellationToken);
    }

    private static async Task WritePakEntry(
        ZipArchive archive,
        StraumrWorkspaceEntry entry,
        string name,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry pakEntry = archive.CreateEntry(".pak", CompressionLevel.SmallestSize);
        await using Stream pakStream = await pakEntry.OpenAsync();
        await using StreamWriter writer = new(pakStream, Encoding.UTF8);

        await writer.WriteLineAsync(entry.Id.ToString().AsMemory(), cancellationToken);
        await writer.WriteLineAsync(name.AsMemory(), cancellationToken);
    }

    private static async Task WriteWorkspaceFiles(
        ZipArchive archive,
        string directoryName,
        CancellationToken cancellationToken)
    {
        string baseFolderName =
            Path.GetFileName(directoryName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        foreach (string filePath in Directory.EnumerateFiles(directoryName, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(Path.GetFileName(filePath), ".pak", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(directoryName, filePath);
            string entryPath = Path.Combine(baseFolderName, relativePath).Replace('\\', '/');
            await archive.CreateEntryFromFileAsync(
                filePath, entryPath, CompressionLevel.SmallestSize, cancellationToken);
        }
    }
}
