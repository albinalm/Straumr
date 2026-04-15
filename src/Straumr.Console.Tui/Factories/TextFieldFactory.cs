using Straumr.Console.Tui.Components.TextFields;
using Straumr.Console.Tui.Helpers;

namespace Straumr.Console.Tui.Factories;

internal static class TextFieldFactory
{
    public static InteractiveTextField CreateFilterField(Action<string> onChanged, Action acceptFilter)
    {
        var field = new InteractiveTextField();

        field.TextChanged += (_, _) => onChanged(field.Text);

        field.Bind(TextFieldKeyBinding.When(
            (_, key) => KeyHelpers.IsTabNavigation(key) || KeyHelpers.IsCursorDown(key),
            (_, _) =>
            {
                acceptFilter();
                return true;
            }));

        return field;
    }

    public static InteractiveTextField CreatePromptField(Action onChanged)
    {
        var field = new InteractiveTextField();

        field.TextChanged += (_, _) => onChanged();

        return field;
    }
}
