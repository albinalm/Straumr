using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Visuals.Shared;

internal static class StraumrStyles
{
    /// <summary>
    /// The mockup's surfaces sit near hue 202, which reads as teal in a terminal. The ramp below keeps
    /// the mockup's surface relationships and contrast but rotates them to hue 222 for a deeper blue.
    /// </summary>
    public static readonly Color Background = Hex(0x0F1420);

    public static readonly Color Panel = Hex(0x151C2C);
    public static readonly Color PanelAlt = Hex(0x0B0F19);
    public static readonly Color Selection = Hex(0x22315E);
    public static readonly Color Border = Hex(0x2B3654);
    public static readonly Color BorderStrong = Hex(0x46567D);
    public static readonly Color Text = Hex(0xD9E0F0);
    public static readonly Color Muted = Hex(0x8B95AD);
    public static readonly Color Accent = Hex(0x5C9CFF);
    public static readonly Color Yellow = Hex(0xE8C36B);

    /// <summary>The mockup renders the active-workspace label at 76% opacity over the shell background.</summary>
    public static readonly Color GreenSubdued = Hex(0x5BA685);

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
        PrimaryText with { Foreground = GreenSubdued };

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

    public static readonly GroupStyle WindowGroup = GroupStyle.Single with
    {
        BorderCellStyle = Style.None.WithForeground(Border),
        FocusedBorderCellStyle = Style.None.WithForeground(Border),
        LabelBackgroundStyle = Style.None.WithBackground(Background),
        BackgroundStyle = Style.None.WithBackground(Background)
    };

    public static readonly ScrollViewerStyle ListScrollViewer = ScrollViewerStyle.Default with
    {
        TrackStyle = Style.None.WithForeground(Border).WithBackground(PanelAlt),
        ThumbStyle = Style.None.WithForeground(BorderStrong).WithBackground(PanelAlt)
    };

    public static readonly RuleStyle Divider = RuleStyle.Default with
    {
        LineStyle = Style.None.WithForeground(Border)
    };

    public static readonly Style DividerCell = Style.None.WithForeground(Border);

    public static readonly Style SelectedItem = Style.None
        .WithForeground(Text)
        .WithBackground(Selection);

    public static readonly Style SelectionMarker = Style.None
        .WithForeground(Accent)
        .WithBackground(Selection);

    private static Color Hex(uint rgb) =>
        Color.Rgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
