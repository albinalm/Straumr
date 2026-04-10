using Straumr.Console.Tui.Components.Base;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Straumr.Console.Tui.Screens.Base;

public abstract class Screen
{
    private readonly List<TuiComponent> _components = [];

    protected T Add<T>(T component) where T : TuiComponent
    {
        _components.Add(component);
        return component;
    }

    protected TView AddView<TView>(TView view) where TView : View
    {
        _components.Add(new InlineComponent(view));
        return view;
    }

    internal IReadOnlyList<TuiComponent> Components => _components;
    internal Action? QuitAction { get; set; }
    internal Action<Type>? NavigateAction { get; set; }
    internal Func<Task>? PendingExternalAction { get; private set; }

    protected void Quit() => QuitAction?.Invoke();

    protected void RequestExternalAndRefresh(Func<Task> action)
    {
        PendingExternalAction = action;
        Quit();
    }

    protected void NavigateTo<TScreen>() where TScreen : Screen
    {
        if (NavigateAction is null)
        {
            return;
        }

        NavigateAction(typeof(TScreen));
        Quit();
    }

    public virtual Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public virtual bool OnKeyDown(Key key) => false;

    private sealed class InlineComponent(View view) : TuiComponent
    {
        public override View Build() => view;
    }
}
