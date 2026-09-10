using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Visuals.Shared;

internal static class StraumrStyles
{
    public static readonly Color Background = Color.Rgb(13, 28, 37);
    public static readonly Color Panel = Color.Rgb(16, 35, 46);
    public static readonly Color PanelAlt = Color.Rgb(11, 25, 34);
    public static readonly Color Text = Color.Rgb(216, 230, 238);
    public static readonly Color Muted = Color.Rgb(137, 161, 175);
    public static readonly Color Border = Color.Rgb(54, 80, 94);
    public static readonly Color Accent = Color.Rgb(82, 168, 255);
    public static readonly Color Green = Color.Rgb(117, 212, 165);
    public static readonly Color Yellow = Color.Rgb(232, 195, 107);
    public static readonly Color Selection = Color.Rgb(23, 54, 76);

    public static readonly TextBlockStyle PrimaryText =
        TextBlockStyle.Default with
        {
            Foreground = Text,
            TextStyle = TextStyle.None
        };

    public static readonly TextBlockStyle MutedText =
        PrimaryText with { Foreground = Muted };

    public static readonly TextBlockStyle AccentText =
        PrimaryText with { Foreground = Accent };

    public static readonly TextBlockStyle GreenText =
        PrimaryText with { Foreground = Green };

    public static readonly TextBlockStyle YellowText =
        PrimaryText with { Foreground = Yellow };

    public static readonly CommandBarStyle CommandBar =
        CommandBarStyle.Default with
        {
            Background = Background,
            Foreground = Muted,
            KeyForeground = Accent,
            KeyBackground = Background,
            Separator = "   ",
            KeycapOpen = new Rune(' '),
            KeycapClose = new Rune(' ')
        };

    public static readonly GroupStyle ListGroup = CreateGroupStyle(PanelAlt);
    public static readonly GroupStyle DetailGroup = CreateGroupStyle(Panel);
    public static readonly GroupStyle ShellGroup = CreateGroupStyle(Background);

    public static readonly Style SelectedItem = Style.None
        .WithForeground(Text)
        .WithBackground(Selection);

    public static readonly Style SelectionMarker = Style.None
        .WithForeground(Accent)
        .WithBackground(Selection);

    private static GroupStyle CreateGroupStyle(Color background) =>
        GroupStyle.Single with
        {
            BorderCellStyle = Style.None.WithForeground(Border),
            FocusedBorderCellStyle = Style.None.WithForeground(Border),
            LabelBackgroundStyle = Style.None.WithBackground(background),
            BackgroundStyle = Style.None.WithBackground(background)
        };
}
