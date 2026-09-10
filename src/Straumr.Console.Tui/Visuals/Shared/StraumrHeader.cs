using Straumr.Console.Tui.Infrastructure;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Visuals.Shared;

internal static class StraumrHeader
{
    /// <summary>
    /// One line of text can only sit centred in an odd-height band, so the bar is a single row and
    /// the window frame above and the rule below do the separating.
    /// </summary>
    private static readonly Thickness BarInset = new(1, 0, 1, 0);

    public static Visual Create(
        State<TuiScreen> currentScreen,
        State<string?> activeWorkspaceName)
    {
        Visual bar = StraumrSurfaces.Bar(
            new HStack(
                    new TextBlock("{straumr}").Style(StraumrStyles.AccentText),
                    new TextBlock("·").Style(StraumrStyles.MutedText),
                    new TextBlock(() => ScreenName(currentScreen.Value)).Style(StraumrStyles.PrimaryText))
                .Spacing(1),
            new HStack(
                    new TextBlock("active workspace").Style(StraumrStyles.MutedText),
                    new TextBlock(() => activeWorkspaceName.Value ?? "none").Style(StraumrStyles.GreenText))
                .Spacing(1));

        return StraumrSurfaces.Inset(bar, BarInset);
    }

    private static string ScreenName(TuiScreen screen) =>
        screen.ToString().ToLowerInvariant();
}
