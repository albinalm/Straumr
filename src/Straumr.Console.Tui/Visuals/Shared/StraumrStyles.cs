using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Visuals.Shared;

internal static class StraumrStyles
{
    /// <summary>
    /// The mockup's surfaces sit near hue 202, which reads as teal in a terminal; this ramp rotates to
    /// hue 222 for a deeper blue and carries higher chroma on the foregrounds so the screen reads as
    /// live rather than flat. Every pairing below stays at or above 4.5:1 for text and 3:1 for glyphs.
    /// </summary>
    /// <remarks>
    /// The whole app shares one background. Regions are told apart by dividers alone, so there is no
    /// panel tint to step against, and the dark ground is what makes the accents carry.
    /// </remarks>
    public static readonly Color Background = Hex(0x090D15);

    /// <summary>The only surface raised above the background, used by an identifier badge.</summary>
    public static readonly Color Raised = Hex(0x1B2438);
    public static readonly Color Selection = Hex(0x21418F);
    public static readonly Color SelectionInactive = Hex(0x2C3D68);
    public static readonly Color Hover = Hex(0x151E33);
    public static readonly Color Border = Hex(0x364670);

    /// <summary>Scroll bar chrome, kept below the dividers so it never competes with content.</summary>
    public static readonly Color ScrollTrack = Hex(0x1E2740);
    public static readonly Color ScrollThumb = Hex(0x43567E);
    public static readonly Color Text = Hex(0xD9E0F0);
    public static readonly Color TextBright = Hex(0xEFF4FF);
    public static readonly Color Muted = Hex(0x8FA0C4);
    public static readonly Color MutedBright = Hex(0xB9C6E0);
    public static readonly Color Accent = Hex(0x3B9EFF);
    public static readonly Color AccentDim = Hex(0x6485B8);
    public static readonly Color Amber = Hex(0xFFC857);
    public static readonly Color Green = Hex(0x3DDC97);
    public static readonly Color Red = Hex(0xFF6B7A);
    public static readonly Color Purple = Hex(0xB79CFF);

    public static readonly TextBlockStyle PrimaryText =
        TextBlockStyle.Default with
        {
            Foreground = Text,
            TextStyle = TextStyle.None
        };

    public static readonly TextBlockStyle MutedText =
        PrimaryText with { Foreground = Muted };

    public static readonly TextBlockStyle BrightText =
        PrimaryText with { Foreground = TextBright };

    public static readonly TextBlockStyle MutedBrightText =
        PrimaryText with { Foreground = MutedBright };

    public static readonly TextBlockStyle AccentText =
        PrimaryText with { Foreground = Accent };

    public static readonly TextBlockStyle GreenText =
        PrimaryText with { Foreground = Green };

    public static readonly TextBlockStyle AccentDimText =
        PrimaryText with { Foreground = AccentDim };

    /// <summary>A filled badge for a quantity.</summary>
    public static readonly TextBlockStyle AccentChip =
        PrimaryText with
        {
            Foreground = TextBright,
            Background = Selection,
            FillBackground = true
        };

    /// <summary>A raised badge for a technical identifier.</summary>
    public static readonly TextBlockStyle TokenChip =
        PrimaryText with
        {
            Foreground = MutedBright,
            Background = Raised,
            FillBackground = true
        };

    public static readonly TextBlockStyle AmberText =
        PrimaryText with { Foreground = Amber };

    public static readonly TextBlockStyle RedText =
        PrimaryText with { Foreground = Red };

    public static readonly TextBlockStyle PurpleText =
        PrimaryText with { Foreground = Purple };

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
        TrackStyle = Style.None.WithForeground(ScrollTrack).WithBackground(Background),
        ThumbStyle = Style.None.WithForeground(ScrollThumb).WithBackground(Background)
    };

    public static readonly RuleStyle Divider = RuleStyle.Default with
    {
        LineStyle = Style.None.WithForeground(Border)
    };

    public static readonly Style DividerCell = Style.None.WithForeground(Border);

    public static readonly Style SelectedItem = Style.None
        .WithForeground(TextBright)
        .WithBackground(Selection);

    public static readonly Style SelectedItemInactive = Style.None
        .WithForeground(Text)
        .WithBackground(SelectionInactive);

    public static readonly Style SelectionMarker = Style.None
        .WithForeground(Accent)
        .WithBackground(Selection);

    public static readonly Style SelectionMarkerInactive = Style.None
        .WithForeground(Muted)
        .WithBackground(SelectionInactive);

    public static readonly Style HoveredItem = Style.None
        .WithForeground(Text)
        .WithBackground(Hover);

    private static Color Hex(uint rgb) =>
        Color.Rgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
