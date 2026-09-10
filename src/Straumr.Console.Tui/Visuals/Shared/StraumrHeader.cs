using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;

namespace Straumr.Console.Tui.Visuals.Shared;

internal static class StraumrHeader
{
    public static Visual Create(
        State<Infrastructure.TuiScreen> currentScreen,
        State<string?> activeWorkspaceName)
    {
        var content = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() },
                new ColumnDefinition { Width = GridLength.Auto })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Cell(
                new HStack(
                    new TextBlock("{straumr}")
                        .Style(StraumrStyles.AccentText),
                    new TextBlock(() => ScreenName(currentScreen.Value))
                        .Style(StraumrStyles.PrimaryText))
                    .Spacing(1),
                0,
                0)
            .Cell(
                new HStack(
                    new TextBlock("active workspace")
                        .Style(StraumrStyles.MutedText),
                    new TextBlock(() => activeWorkspaceName.Value ?? "none")
                        .Style(StraumrStyles.GreenText))
                    .Spacing(1),
                0,
                2)
            .HorizontalAlignment(Align.Stretch);

        var frame = new Group()
            .Padding(new Thickness(1, 0, 1, 0))
            .Content(content)
            .HorizontalAlignment(Align.Stretch);
        frame.SetStyle(StraumrStyles.ShellGroup);
        return frame;
    }

    private static string ScreenName(Infrastructure.TuiScreen screen) =>
        screen.ToString().ToLowerInvariant();
}
