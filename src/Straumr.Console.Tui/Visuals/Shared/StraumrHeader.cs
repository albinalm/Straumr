using Straumr.Console.Tui.Infrastructure;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Visuals.Shared;

internal static class StraumrHeader
{
    public static Visual Create(
        State<TuiScreen> currentScreen,
        State<string?> activeWorkspaceName) =>
        Create(() => ScreenName(currentScreen.Value), () => activeWorkspaceName.Value);

    /// <summary>
    /// The shell's identity line, which a full-screen view builds for itself so it reads as a screen
    /// of the app rather than as a dialog over one.
    /// </summary>
    /// <param name="screenName">
    /// What the line names after <c>{straumr}</c>: the screen, for example <c>requests</c>, or for a
    /// view opened from one resource, that resource. A view of one thing is better named by the thing
    /// than by the kind of view it is.
    /// </param>
    /// <remarks>
    /// One line of text can only sit centred in an odd-height band, so the bar is a single row and
    /// the window frame above and the rule below do the separating.
    /// </remarks>
    public static Visual Create(
        Func<string> screenName,
        Func<string?> activeWorkspaceName)
    {
        var identity = new HStack(
                new TextBlock("{straumr}").Style(StraumrStyles.AccentText),
                new TextBlock("·").Style(StraumrStyles.MutedText),
                new TextBlock(screenName).Style(StraumrStyles.PrimaryText))
            .Spacing(1);

        Visual bar = StraumrSurfaces.Bar(
            identity,
            new HStack(
                    new TextBlock("active workspace").Style(StraumrStyles.MutedText),
                    new TextBlock(() => activeWorkspaceName() ?? "none").Style(StraumrStyles.GreenText))
                .Spacing(1));

        return StraumrSurfaces.Inset(bar, StraumrSurfaces.RowInset);
    }

    private static string ScreenName(TuiScreen screen) =>
        screen.ToString().ToLowerInvariant();
}
