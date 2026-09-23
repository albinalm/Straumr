using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Models;

internal sealed record PreviewPanePageModel(string Title)
{
    public Visual? Content { get; init; }
    public Func<Visual>? FocusTarget { get; init; }

    public static PreviewPanePageModel Text(string title) => new(title);

    public static PreviewPanePageModel Custom(string title, Visual content, Func<Visual> focusTarget) =>
        new(title) { Content = content, FocusTarget = focusTarget };
}
