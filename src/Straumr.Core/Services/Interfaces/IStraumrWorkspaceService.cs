using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrWorkspaceService
{
    Task Activate(string name);
    Task CreateAndOpen(StraumrWorkspace workspace);
    Task Import(string path);
    Task Delete(string identifier);
    Task Save();
    Task<string> Export(string workspaceName, string outputDir);
    Task<string> PrepareEdit(string name);
    Task ApplyEdit(string name, string tempPath);
    Task<string> GetWorkspaceName(string path);
    Task<StraumrWorkspace> GetWorkspace(string path);
}