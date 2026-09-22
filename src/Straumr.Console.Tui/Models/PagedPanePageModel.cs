using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Models;

internal sealed record PagedPanePageModel(string Title, Visual Content, Func<Visual> FocusTarget)
{
    public Func<bool>? Visible { get; init; }

    public bool Applies => Visible?.Invoke() ?? true;
}
