using Straumr.Core.Models;

namespace Straumr.Console.Tui.Models;

internal sealed record AuthScreenItemModel(Guid Id, string Path, StraumrAuth? Auth, string? Problem = null)
{
    public string Name => Auth?.Name ?? Id.ToString()[..8];

    public bool IsBroken => Auth is null;
}
