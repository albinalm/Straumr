using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.Prompts.Selection;

internal sealed class FilterTextField : TextField
{
    private readonly Action<string> _onChanged;
    private readonly Action _acceptFilter;
    private readonly Action _exitFilter;
    private readonly Action? _cancelRequested;

    public FilterTextField(Action<string> onChanged, Action acceptFilter, Action exitFilter, Action? cancelRequested = null)
    {
        _onChanged = onChanged;
        _acceptFilter = acceptFilter;
        _exitFilter = exitFilter;
        _cancelRequested = cancelRequested;

        TextChanged += (_, _) => _onChanged(Text ?? string.Empty);
    }

    protected override bool OnKeyDown(Key key)
    {
        if (key == Key.Enter)
        {
            _acceptFilter();
            return true;
        }

        if (key == Key.Esc)
        {
            Text = string.Empty;
            _exitFilter();
            return true;
        }

        if (key == Key.Tab || key == Key.Tab.WithShift || key == Key.CursorDown)
        {
            _acceptFilter();
            return true;
        }

        return base.OnKeyDown(key);
    }
}
