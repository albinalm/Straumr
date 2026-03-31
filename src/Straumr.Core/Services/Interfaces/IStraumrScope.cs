using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrScope
{
    StraumrWorkspace? Workspace { get; set; }
}