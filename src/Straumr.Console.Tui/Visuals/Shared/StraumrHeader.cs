using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Visuals.Shared;

internal static class StraumrHeader
{
    private static readonly TextBlockStyle ActiveWorkspaceStyle =
        TextBlockStyle.Default with { Foreground = Colors.Green };

    public static Header Create(
        State<Infrastructure.TuiScreen> currentScreen,
        State<string?> activeWorkspaceName)
    {
        return new Header
        {
            Left = new HStack(
                    new TextBlock("{straumr}"),
                    new TextBlock(() => ScreenName(currentScreen.Value)))
                .Spacing(1),
            Right = new HStack(
                    new TextBlock("active workspace"),
                    new TextBlock(() => activeWorkspaceName.Value ?? "none")
                        .Style(ActiveWorkspaceStyle))
                .Spacing(1)
        };
    }

    private static string ScreenName(Infrastructure.TuiScreen screen) =>
        screen.ToString().ToLowerInvariant();
}
