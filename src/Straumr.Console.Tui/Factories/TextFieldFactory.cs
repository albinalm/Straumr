using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Helpers;
using Terminal.Gui.Input;

namespace Straumr.Console.Tui.Factories;

internal static class TextFieldFactory
{
    public static InteractiveTextField CreateFilterField(Action<string> onChanged, Action acceptFilter, Action exitFilter)
    {
        var field = new InteractiveTextField();

        field.TextChanged += (_, _) => onChanged(field.Text);

        field.Bind(TextFieldKeyBinding.When(
            (_, key) => KeyHelpers.IsEnter(key),
            (_, _) =>
        {
            acceptFilter();
            return true;
        }));

        field.Bind(TextFieldKeyBinding.When(
            (_, key) => KeyHelpers.IsEscape(key),
            (f, _) =>
        {
            f.Text = string.Empty;
            exitFilter();
            return true;
        }, clearText: true));

        field.Bind(TextFieldKeyBinding.When(
            (_, key) => KeyHelpers.IsTabNavigation(key) || KeyHelpers.IsCursorDown(key),
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

        field.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsEnter(key), (_, _) => submit()));
        field.Bind(TextFieldKeyBinding.When((_, key) => KeyHelpers.IsEscape(key), (_, _) => requestCancel()));
        field.TextChanged += (_, _) => onChanged();

        return field;
    }
}
