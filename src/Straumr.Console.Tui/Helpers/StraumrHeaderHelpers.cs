using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Helpers;

internal static class StraumrHeaderHelpers
{
    private const string MenuGlyph = "☰";

    public static Visual Create(
        State<TuiScreen> currentScreen,
        State<string?> activeWorkspaceName,
        Func<IEnumerable<MenuItem>> screenMenu) =>
        Create(
            () => ScreenName(currentScreen.Value),
            () => activeWorkspaceName.Value,
            screenMenu);

    public static Visual Create(
        Func<string> screenName,
        Func<string?> activeWorkspaceName,
        Func<IEnumerable<MenuItem>>? screenMenu = null)
    {
        Visual[] identity =
        [
            .. screenMenu is null ? (Visual[])[] : [MenuButton(screenMenu)],
            new TextBlock("{straumr}").Style(StraumrStyleService.BrandText),
            new TextBlock("·").Style(StraumrStyleService.MutedText),
            new TextBlock(screenName).Style(StraumrStyleService.PrimaryText)
        ];

        Visual bar = StraumrSurfaceHelpers.Bar(
            new HStack(identity).Spacing(1),
            new HStack(
                    new TextBlock("active workspace").Style(StraumrStyleService.MutedText),
                    new TextBlock(() => activeWorkspaceName() ?? "none").Style(StraumrStyleService.GreenText))
                .Spacing(1));

        return StraumrSurfaceHelpers.Inset(bar, StraumrSurfaceHelpers.RowInset);
    }

    private static Visual MenuButton(Func<IEnumerable<MenuItem>> screenMenu)
    {
        var button = new ChromeButton(MenuGlyph);
        button.SetStyle(StraumrStyleService.Button);
        button.SetStyle(StraumrStyleService.Menu);
        button.Click(() => ContextMenuService.Show(
            button, screenMenu(), button.Bounds.X, button.Bounds.Y));

        return button;
    }

    private static string ScreenName(TuiScreen screen) =>
        screen.ToString().ToLowerInvariant();
}
