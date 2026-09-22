namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class PaneSplits
{
    public PaneSplits(int panels = 31, int sections = 48, int stack = 50)
    {
        Panels = new PaneSplit(panels);
        Sections = new PaneSplit(sections);
        Stack = new PaneSplit(stack);
        Panels.Changed += OnChanged;
        Sections.Changed += OnChanged;
        Stack.Changed += OnChanged;
    }

    public PaneSplit Panels { get; }

    public PaneSplit Sections { get; }

    public PaneSplit Stack { get; }

    public bool HasStack { get; private set; }

    public event Action? Changed;

    internal void UseStack() => HasStack = true;

    private void OnChanged() => Changed?.Invoke();
}
