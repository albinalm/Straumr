using Straumr.Core.Models;

namespace Straumr.Console.Tui.Models;

internal sealed record SecretScreenItemModel(Guid Id, string Path, StraumrSecret? Secret, string? Problem = null)
{
    public string Name => Secret?.Name ?? Id.ToString()[..8];
    public bool IsBroken => Secret is null;
}
