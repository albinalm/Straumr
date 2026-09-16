using Straumr.Core.Models;

namespace Straumr.Console.Tui.Screens.Auth;

/// <param name="Auth">
/// The auth as Core read it, or <see langword="null"/> for a file that could not be loaded into
/// fields at all. A broken auth keeps its row so it can be found and repaired.
/// </param>
internal sealed record AuthScreenItem(Guid Id, string Path, StraumrAuth? Auth, string? Problem = null)
{
    public string Name => Auth?.Name ?? Id.ToString()[..8];

    public bool IsBroken => Auth is null;
}
