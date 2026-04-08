using Straumr.Console.Tui.Components.Base;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Screens.Base;

public abstract class Screen
{
    private readonly List<TuiComponent> _components = [];

    protected T Add<T>(T component) where T : TuiComponent
    {
        _components.Add(component);
        return component;
    }

    internal IReadOnlyList<TuiComponent> Components => _components;
    internal Action? QuitAction { get; set; }

    protected void Quit() => QuitAction?.Invoke();

    public virtual bool OnKeyDown(Key key) => false;
}
