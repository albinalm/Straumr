using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrWorkspaceService
{
    Task Activate(string identifier);
    Task Create(StraumrWorkspace workspace, string? outputDir = null);
    Task<StraumrWorkspaceEntry> Copy(string identifier, string newName, string? outputDir = null);
    Task<StraumrWorkspaceEntry> Import(string path);
    Task Delete(string identifier);
    Task<string> Export(string workspaceIdentifier, string outputDir);
    Task<string> PrepareEdit(string identifier);
    Task ApplyEdit(string identifier, string tempPath);
    Task<StraumrWorkspace> GetWorkspace(string path);
    Task<StraumrWorkspace> PeekWorkspace(string path);
    StraumrWorkspaceEntry GetWorkspaceEntry(Guid id);
}
