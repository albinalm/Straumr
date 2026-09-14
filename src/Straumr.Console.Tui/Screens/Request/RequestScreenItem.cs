using Straumr.Core.Models;

namespace Straumr.Console.Tui.Screens.Request;

internal sealed record RequestScreenItem(Guid Id, string Path, StraumrRequest? Request, string? Problem = null)
{
    public string Name => Request?.Name ?? Id.ToString()[..8];
    public bool IsBroken => Request is null;
}
