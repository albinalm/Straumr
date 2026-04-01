using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrWorkspaceService
{
    Task Activate(string name);
    Task CreateAndOpen(StraumrWorkspace workspace);
    Task Import(string path);
    Task Delete(string identifier);
    Task<string> Export(string workspaceName, string outputDir);
    Task<string> PrepareEdit(string identifier);
    Task ApplyEdit(string identifier, string tempPath);
    Task<StraumrWorkspace> GetWorkspace(string path);
}