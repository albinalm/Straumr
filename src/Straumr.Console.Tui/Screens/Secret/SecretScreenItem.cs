using Straumr.Core.Models;

namespace Straumr.Console.Tui.Screens.Secret;

internal sealed record SecretScreenItem(Guid Id, string Path, StraumrSecret? Secret, string? Problem = null)
{
    public string Name => Secret?.Name ?? Id.ToString()[..8];
    public bool IsBroken => Secret is null;
}
