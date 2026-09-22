using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Helpers;

internal static class EditorVisualHelpers
{
    public static ValidationPresenter Validated(Visual input)
    {
        var presenter = new ValidationPresenter(input)
        {
            Placement = ValidationPlacement.Below
        };
        presenter.SetStyle(StraumrStyleService.Validation);
        return presenter;
    }
}
