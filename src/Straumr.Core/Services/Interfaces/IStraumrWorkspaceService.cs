using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrWorkspaceService
{
    Task Load(string name);
    Task CreateAndOpen(StraumrWorkspace workspace);
    Task Import(string path);
    Task Save();
}