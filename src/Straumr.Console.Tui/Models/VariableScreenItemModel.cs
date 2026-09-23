using Straumr.Core.Models;

namespace Straumr.Console.Tui.Models;

internal sealed record VariableScreenItemModel(Guid Id, string Path, StraumrVariable? Variable, string? Problem = null)
{
    public string Name => Variable?.Name ?? Id.ToString()[..8];
    public bool IsBroken => Variable is null;
}
