using Straumr.Console.Tui.Components.TextFields;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Factories;

internal static class TextFieldFactory
{
    public static InteractiveTextField CreateFilterField(Action<string> onChanged, Action acceptFilter, Action exitFilter)
    {
        var field = new InteractiveTextField();

        field.TextChanged += (_, _) => onChanged(field.Text);

        field.Bind(Key.Enter, (_, _) =>
        {
            acceptFilter();
            return true;
        });

        field.Bind(Key.Esc, (f, _) =>
        {
            f.Text = string.Empty;
            exitFilter();
            return true;
        }, clearText: true);

        field.Bind(TextFieldKeyBinding.When(
            (_, key) => key == Key.Tab || key == Key.Tab.WithShift || key == Key.CursorDown,
            (_, _) =>
            {
                acceptFilter();
                return true;
            }));

        return field;
    }

    public static InteractiveTextField CreatePromptField(Action onChanged, Func<bool> submit, Func<bool> requestCancel)
    {
        var field = new InteractiveTextField();

        field.Bind(Key.Enter, (_, _) => submit());
        field.Bind(Key.Esc, (_, _) => requestCancel());
        field.Bind(Key.C.WithCtrl, (_, _) => requestCancel());
        field.TextChanged += (_, _) => onChanged();

        return field;
    }
}
