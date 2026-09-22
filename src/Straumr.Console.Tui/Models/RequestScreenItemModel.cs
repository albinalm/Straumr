using Straumr.Core.Models;

namespace Straumr.Console.Tui.Models;

internal sealed record RequestScreenItemModel(Guid Id, string Path, StraumrRequest? Request, string? Problem = null)
{
    public string Name => Request?.Name ?? Id.ToString()[..8];
    public bool IsBroken => Request is null;
}
