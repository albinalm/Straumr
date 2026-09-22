using XenoAtom.Terminal.UI;

namespace Straumr.Console.Tui.Screens.Components.Editor;

internal abstract class EditorField
{
    protected EditorField(string label)
    {
        Label = label;
    }

    public string Label { get; }

    public Func<bool>? Visible { get; init; }

    public bool IsVisible => Visible?.Invoke() ?? true;

    public bool GrowsToFill { get; protected init; }

    public abstract Visual Content { get; }

    public abstract Visual FocusTarget { get; }

    public Action? Changed { get; set; }

    public Action? OnCommit { get; init; }

    public virtual void Commit() => OnCommit?.Invoke();

    public virtual string? Validate() => null;

    public virtual void ShowProblem(string message) { }

    public virtual void ClearProblem() { }
}
