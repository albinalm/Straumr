using Straumr.Core.Models;

namespace Straumr.Core.Services.Interfaces;

public interface IStraumrOptionsService
{
    StraumrOptions Options { get; }
    Task Load();
    Task Save();
}
