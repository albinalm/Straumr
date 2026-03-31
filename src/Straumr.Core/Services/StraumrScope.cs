using Straumr.Core.Models;
using Straumr.Core.Services.Interfaces;

namespace Straumr.Core.Services;

public class StraumrScope : IStraumrScope
{
    public StraumrWorkspace? Workspace { get; set; }
}