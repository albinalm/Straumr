using Straumr.Console.Tui.Components.TextFields.Base;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Straumr.Console.Tui.Components.TextFields;

internal sealed class FilterTextField : ThemedTextField
{
    private readonly Action _acceptFilter;
    private readonly Action _exitFilter;

    public FilterTextField(Action<string> onChanged, Action acceptFilter, Action exitFilter)
    {
        _acceptFilter = acceptFilter;
        _exitFilter = exitFilter;

        TextChanged += (_, _) => onChanged(Text);
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
