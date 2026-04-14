using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrWorkspaceService
{
    Task Activate(string identifier);
    Task Create(StraumrWorkspace workspace, string? outputDir = null);
    Task<StraumrWorkspaceEntry> CopyAsync(string identifier, string newName, string? outputDir = null);
    Task<StraumrWorkspaceEntry> ImportAsync(string path);
    Task DeleteAsync(string identifier);
    Task<string> ExportAsync(string workspaceIdentifier, string outputDir);
    Task<string> PrepareEditAsync(string identifier);
    Task ApplyEditAsync(string identifier, string tempPath);
    Task<StraumrWorkspace> GetWorkspaceAsync(string path);
    Task<StraumrWorkspace> PeekWorkspaceAsync(string path);
    StraumrWorkspaceEntry GetWorkspaceEntryOnDisk(Guid id);
}
