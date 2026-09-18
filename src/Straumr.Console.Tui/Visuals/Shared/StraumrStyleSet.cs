using System.Text;
using Straumr.Console.Tui.Visuals.Theming;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// Every control style the shell paints with, built from one <see cref="StraumrPalette"/>.
/// </summary>
/// <remarks>
/// Styles are built once per theme rather than resolved per use because the framework's own styles
/// are immutable values a control is handed at construction, not bindings it re-reads. That is also
/// why changing the theme rebuilds the visual tree rather than repainting it: a style already given
/// to a control cannot be taken back. <see cref="StraumrStyles"/> is the facade the rest of the app
/// reads this through, so nothing outside these two files needs to know a theme exists.
/// </remarks>
internal sealed class StraumrStyleSet
{
    public StraumrPalette Palette { get; }

    /// <summary>
    /// The framework theme the shell is hosted under, which decides what every cell no Straumr
    /// style reaches is painted with — the ground behind the whole app included.
    /// </summary>
    /// <remarks>
    /// A theme whose background is the terminal's own gets <see cref="Theme.Terminal"/>, whose
    /// <c>Background</c> and <c>Foreground</c> are null: the renderer then emits no colour for
    /// those cells at all, so a transparent terminal stays transparent. Without it the framework
    /// clears every cell with <see cref="Theme.Default"/>'s own dark blue, which is what made the
    /// first terminal theme a solid navy rectangle sitting on top of the reader's wallpaper.
    /// A theme that names a concrete background keeps <see cref="Theme.Default"/>, because the
    /// window group then fills the whole surface with that background and nothing the framework
    /// theme would have painted is ever visible.
    /// </remarks>
    public Theme FrameworkTheme { get; }

    public Color Background => Palette.Background;
    public Color Raised => Palette.Raised;
    public Color Selection => Palette.Selection;
    public Color SelectionInactive => Palette.SelectionInactive;
    public Color Hover => Palette.Hover;
    public Color Border => Palette.Border;
    public Color ScrollTrack => Palette.ScrollTrack;
    public Color ScrollThumb => Palette.ScrollThumb;
    public Color Text => Palette.Text;
    public Color TextBright => Palette.TextBright;
    public Color Muted => Palette.Muted;
    public Color MutedBright => Palette.MutedBright;
    public Color Accent => Palette.Accent;
    public Color Amber => Palette.Amber;
    public Color Green => Palette.Green;
    public Color Red => Palette.Red;
    public Color RedBright => Palette.RedBright;
    public Color Purple => Palette.Purple;
    public Color Brand => Palette.Brand;

    public TextBlockStyle PrimaryText { get; }
    public TextBlockStyle MutedText { get; }
    public TextBlockStyle BrightText { get; }
    public TextBlockStyle MutedBrightText { get; }
    public TextBlockStyle AccentText { get; }
    public TextBlockStyle GreenText { get; }
    public TextBlockStyle FocusChip { get; }
    public TextBlockStyle TokenChip { get; }
    public TextBlockStyle AmberText { get; }
    public TextBlockStyle RedText { get; }
    public TextBlockStyle RedBrightText { get; }
    public TextBlockStyle PurpleText { get; }
    public TextBlockStyle BrandText { get; }

    /// <summary>The style each HTTP method reads in, keyed the way the request names it.</summary>
    public TextBlockStyle MethodText(string method) => method.ToUpperInvariant() switch
    {
        "GET" => _get,
        "POST" => _post,
        "PUT" => _put,
        "PATCH" => _patch,
        "DELETE" => _delete,
        _ => _otherMethod
    };

    public Style CodeKey { get; }
    public Style CodeString { get; }
    public Style CodeNumber { get; }
    public Style CodeBoolean { get; }
    public Style CodeNull { get; }
    public Style CodePunctuation { get; }
    public Style CodePlain { get; }
    public Style CodeNote { get; }

    private readonly TextBlockStyle _get;
    private readonly TextBlockStyle _post;
    private readonly TextBlockStyle _put;
    private readonly TextBlockStyle _patch;
    private readonly TextBlockStyle _delete;
    private readonly TextBlockStyle _otherMethod;
    public CommandBarStyle CommandBar { get; }
    public TabControlStyle PreviewTabs { get; }
    public Style CommandPromptText { get; }
    public PromptEditorStyle CommandPrompt { get; }
    public ButtonStyle Button { get; }
    public ButtonStyle DangerButton { get; }
    public ButtonStyle PrimaryButton { get; }
    public ButtonStyle RuleTab { get; }
    public ButtonStyle RuleTabSelected { get; }
    public ButtonStyle RuleTabFocused { get; }
    public TextBoxStyle TextBox { get; }
    public SelectStyle Select { get; }
    public SwitchStyle Switch { get; }
    public ValidationStyle Validation { get; }
    public DialogStyle Dialog { get; }
    public GroupStyle WindowGroup { get; }
    public SpinnerStyle RequestPulse { get; }
    public ScrollViewerStyle ListScrollViewer { get; }
    public RuleStyle Divider { get; }
    public Style DividerCell { get; }
    public Style ScrollTrackCell { get; }
    public Style ScrollThumbCell { get; }
    public Style SelectedItem { get; }
    public Style SelectedItemInactive { get; }
    public Style SelectionMarker { get; }
    public Style SelectionMarkerInactive { get; }
    public Style HoveredItem { get; }

    private readonly bool _selectionInverts;

    public StraumrStyleSet(StraumrPalette palette)
    {
        Palette = palette;
        _selectionInverts = palette.SelectionInverts;
        FrameworkTheme = palette.Background.Kind == ColorKind.Default ? Theme.Terminal : Theme.Default;

        PrimaryText = TextBlockStyle.Default with
        {
            Foreground = Text,
            TextStyle = TextStyle.None
        };

        MutedText = PrimaryText with { Foreground = Muted };
        BrightText = PrimaryText with { Foreground = TextBright };
        MutedBrightText = PrimaryText with { Foreground = MutedBright };
        AccentText = PrimaryText with { Foreground = Accent };
        GreenText = PrimaryText with { Foreground = Green };

        // The only filled accent surface on screen: it marks the region that owns focus. It carries
        // the selection band's colour so a fill means "here" on a rule exactly as it does on a row.
        FocusChip = palette.SelectionInverts
            ? PrimaryText with { Foreground = TextBright, TextStyle = TextStyle.Invert, FillBackground = true }
            : PrimaryText with { Foreground = TextBright, Background = Selection, FillBackground = true };

        // A raised badge for a quantity or a technical identifier.
        TokenChip = PrimaryText with
        {
            Foreground = MutedBright,
            Background = Raised,
            FillBackground = true
        };

        AmberText = PrimaryText with { Foreground = Amber };
        RedText = PrimaryText with { Foreground = Red };
        RedBrightText = PrimaryText with { Foreground = RedBright };
        PurpleText = PrimaryText with { Foreground = Purple };
        BrandText = PrimaryText with { Foreground = Brand };

        MethodPalette methods = palette.Methods;
        _get = PrimaryText with { Foreground = methods.Get };
        _post = PrimaryText with { Foreground = methods.Post };
        _put = PrimaryText with { Foreground = methods.Put };
        _patch = PrimaryText with { Foreground = methods.Patch };
        _delete = PrimaryText with { Foreground = methods.Delete };
        _otherMethod = PrimaryText with { Foreground = methods.Other };

        CodePalette code = palette.Code;
        CodeKey = Style.None.WithForeground(code.Key);
        CodeString = Style.None.WithForeground(code.String);
        CodeNumber = Style.None.WithForeground(code.Number);
        CodeBoolean = Style.None.WithForeground(code.Boolean);
        CodeNull = Style.None.WithForeground(code.Null);
        CodePunctuation = Style.None.WithForeground(code.Punctuation);
        CodePlain = Style.None.WithForeground(Text);
        CodeNote = Style.None.WithForeground(Muted);

        CommandBar = CommandBarStyle.Default with
        {
            Background = Background,
            Foreground = Muted,
            KeyForeground = Accent,
            KeyBackground = Background,
            Separator = "   ",
            KeycapOpen = new Rune(' '),
            KeycapClose = new Rune(' ')
        };

        PreviewTabs = TabControlStyle.NoBorder with
        {
            TabPadding = new Thickness(1, 0, 1, 0),
            StripStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            TabStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            TabHoveredStyle = Style.None.WithForeground(TextBright).WithBackground(Hover),
            TabPressedStyle = Style.None.WithForeground(Accent).WithBackground(Hover),
            TabSelectedStyle = Style.None.WithForeground(Accent).WithBackground(Background),
            BorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            FocusedBorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            OverflowButtonStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            OverflowButtonHoveredStyle = Style.None.WithForeground(Accent).WithBackground(Hover),
            OverflowButtonPressedStyle = Style.None.WithForeground(Accent).WithBackground(Hover)
        };

        CommandPromptText = Style.None.WithForeground(TextBright);

        CommandPrompt = PromptEditorStyle.Default with
        {
            Padding = new Thickness(0),
            Background = Background,
            PromptSidebarBackground = Background,
            PromptForeground = Accent,
            GhostForeground = Muted,
            PlaceholderForeground = Muted,
            Selection = Selection,
            ShowPromptSeparator = false
        };

        Button = ButtonStyle.Default with
        {
            Padding = new Thickness(1, 0, 1, 0),
            ShowBorder = false,
            Normal = Style.None.WithForeground(MutedBright).WithBackground(Background),
            Hovered = Style.None.WithForeground(TextBright).WithBackground(Hover),
            Pressed = Style.None.WithForeground(TextBright).WithBackground(SelectionInactive),
            Focused = SelectedSurface(TextBright),
            Disabled = Style.None.WithForeground(Muted).WithBackground(Background)
        };

        DangerButton = Button with
        {
            Normal = Style.None.WithForeground(Red).WithBackground(Background),
            Hovered = Style.None.WithForeground(Red).WithBackground(Hover)
        };

        PrimaryButton = Button with
        {
            Normal = Style.None.WithForeground(Accent).WithBackground(Background),
            Hovered = Style.None.WithForeground(Accent).WithBackground(Hover)
        };

        // A page title notched into the rule above a pane, for a region whose pages are its whole
        // content. It is a button rather than a label so a page stays one click away where the
        // framework's own tab strip would have been, and it never takes focus of its own: the pane
        // below it owns the focus the chip reports.
        RuleTab = Button with
        {
            Normal = Style.None.WithForeground(Muted).WithBackground(Background),
            Hovered = Style.None.WithForeground(TextBright).WithBackground(Hover),
            Pressed = Style.None.WithForeground(Accent).WithBackground(Hover),
            Focused = Style.None.WithForeground(Muted).WithBackground(Background),
            Disabled = Style.None.WithForeground(Muted).WithBackground(Background)
        };

        // The selected page while its pane is not the region that owns focus.
        RuleTabSelected = RuleTab with
        {
            Normal = Style.None.WithForeground(Accent).WithBackground(Background),
            Focused = Style.None.WithForeground(Accent).WithBackground(Background)
        };

        // The selected page while its pane owns focus. It carries the focus chip's fill, so a
        // full-screen view answers "where am I" with the one filled title every screen has.
        RuleTabFocused = RuleTab with
        {
            Normal = SelectedSurface(TextBright),
            Hovered = SelectedSurface(TextBright),
            Focused = SelectedSurface(TextBright)
        };

        TextBox = TextBoxStyle.Default with
        {
            Padding = new Thickness(1, 0, 1, 0),
            Border = Border,
            FocusBorder = Accent,
            Selection = Selection,
            Background = Background,
            ForegroundBrush = SolidOrNull(TextBright),
            BackgroundBrush = SolidOrNull(Background),
            Placeholder = Muted
        };

        // The dropdown a form picks a fixed value with. It borrows the text field's frame so a row
        // of mixed fields reads as one column of inputs rather than as two kinds of control.
        Select = SelectStyle.Default with
        {
            Padding = new Thickness(1, 0, 1, 0),
            NormalStyle = Style.None.WithForeground(TextBright).WithBackground(Background),
            HoverStyle = Style.None.WithForeground(TextBright).WithBackground(Hover),
            FocusedStyle = SelectedSurface(TextBright),
            DisabledStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            PopupTemplateFactory = OpenedSelect
        };

        Switch = SwitchStyle.Round with
        {
            TrackOff = Style.None.WithForeground(Muted).WithBackground(Background),
            TrackOn = Style.None.WithForeground(Accent).WithBackground(Background),
            TrackFocused = SelectedSurface(TextBright),
            TrackHovered = Style.None.WithForeground(TextBright).WithBackground(Hover),
            ThumbOff = Style.None.WithForeground(MutedBright).WithBackground(Background),
            ThumbOn = Style.None.WithForeground(Green).WithBackground(Background)
        };

        Validation = ValidationStyle.Default with
        {
            Padding = new Thickness(0),
            ErrorGlyph = null,
            ErrorStyle = Style.None.WithForeground(Red).WithBackground(Background)
        };

        Dialog = DialogStyle.Single with
        {
            SurfaceStyle = Style.None.WithBackground(Background),
            BorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            FocusedBorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            LabelBackgroundStyle = Style.None.WithBackground(Background)
        };

        WindowGroup = GroupStyle.Single with
        {
            BorderCellStyle = Style.None.WithForeground(Border),
            FocusedBorderCellStyle = Style.None.WithForeground(Border),
            LabelBackgroundStyle = Style.None.WithBackground(Background),
            BackgroundStyle = Style.None.WithBackground(Background)
        };

        RequestPulse = new SpinnerStyle("RequestPulse", TimeSpan.FromMilliseconds(80),
            ["●···········", "·●··········", "··●·········", "···●········", "····●·······", "·····●······",
             "······●·····", "·······●····", "········●···", "·········●··", "··········●·", "···········●",
             "··········●·", "·········●··", "········●···", "·······●····", "······●·····", "·····●······",
             "····●·······", "···●········", "··●·········", "·●··········"])
        { Foreground = Accent };

        ListScrollViewer = ScrollViewerStyle.Default with
        {
            TrackStyle = Style.None.WithForeground(ScrollTrack).WithBackground(Background),
            ThumbStyle = Style.None.WithForeground(ScrollThumb).WithBackground(Background)
        };

        Divider = RuleStyle.Default with
        {
            LineStyle = Style.None.WithForeground(Border)
        };

        DividerCell = Style.None.WithForeground(Border);

        ScrollTrackCell = Style.None
            .WithForeground(ScrollTrack)
            .WithBackground(Background);

        ScrollThumbCell = Style.None
            .WithForeground(ScrollThumb)
            .WithBackground(Background);

        SelectedItem = SelectedSurface(TextBright);

        SelectedItemInactive = Style.None
            .WithForeground(Text)
            .WithBackground(SelectionInactive);

        SelectionMarker = SelectedSurface(Accent);

        SelectionMarkerInactive = Style.None
            .WithForeground(Muted)
            .WithBackground(SelectionInactive);

        HoveredItem = Style.None
            .WithForeground(Text)
            .WithBackground(Hover);
    }

    /// <summary>
    /// A surface carrying the focused selection: the palette's band, or the terminal's own two
    /// colours swapped where the theme asked to inherit them.
    /// </summary>
    /// <remarks>
    /// The inverted form keeps <paramref name="foreground"/> even though inversion would work
    /// without it, and must. A style cannot reset a foreground to the terminal's own — `Color.Default`
    /// is indistinguishable from "unset", so it overwrites nothing — and inversion swaps whatever
    /// colour the cell already carries. An inverted surface with no foreground of its own therefore
    /// inherits one: over a dialog, whose surface fill sets a background but leaves foregrounds
    /// alone, a focused button came out in the colour of whatever text of the screen behind happened
    /// to sit under it. Pinning the foreground makes the band that colour deterministically instead.
    /// </remarks>
    private Style SelectedSurface(Color foreground) =>
        _selectionInverts
            ? Style.None.WithForeground(foreground).WithTextStyle(TextStyle.Invert)
            : Style.None.WithForeground(foreground).WithBackground(Selection);

    /// <summary>
    /// A solid brush, or none at all for the terminal's own colour.
    /// </summary>
    /// <remarks>
    /// <c>Brush.Solid</c> rejects <see cref="Color.Default"/>: a gradient cannot be interpolated
    /// towards a colour the app does not know. The field is nullable and the framework's own default
    /// for it is null, so a theme that defers to the terminal simply does not paint through a brush —
    /// the flat <c>Background</c> colour beside it still carries the value.
    /// </remarks>
    private static Brush? SolidOrNull(Color color) =>
        color.Kind == ColorKind.Default ? null : Brush.Solid(color);

    /// <summary>
    /// The dot marking the current resource in a list, painted over whatever band its row already
    /// carries so it survives selection and hover instead of competing with them. Green is reserved
    /// for the active workspace, so this is the only place it appears outside the header.
    /// </summary>
    public Style CurrentMarker(Color background) =>
        Style.None.WithForeground(Green).WithBackground(background);

    /// <summary>
    /// Text in the command bar's key colour and weight, for a hint that has to name a second key its
    /// one keycap cannot show. The bar parses a label as markup, so this is the one place a colour is
    /// written into text rather than taken from a style; it still comes from the palette. The bold
    /// is what the bar draws its own keycaps with, so the two keys of one hint read as one pair.
    /// </summary>
    public string KeyMarkup(string text) => $"[{MarkupToken(Accent)}][bold]{text}[/][/]";

    /// <summary>
    /// Names a colour in the markup parser's own vocabulary, by kind.
    /// </summary>
    /// <remarks>
    /// <c>ToHexString</c> cannot be used here: it answers a palette colour with a hardcoded xterm
    /// approximation and the terminal default with black, either of which would paint a key hint in
    /// a colour the reader's scheme never chose. An index is written as a bare number, which the
    /// parser reads back as the same palette slot.
    /// </remarks>
    private static string MarkupToken(Color color) => color.Kind switch
    {
        ColorKind.Default => "default",
        ColorKind.Basic16 or ColorKind.Indexed256 => color.Index.ToString(),
        _ => color.ToHexString()
    };

    /// <summary>
    /// The list a dropdown opens. The control builds it itself and hands it here to be framed,
    /// which is the only point at which it can be reached at all; the frame around it is still the
    /// framework's own, and the keys it is given are <see cref="SelectKeys"/>'s.
    /// </summary>
    private static Visual? OpenedSelect(Visual popup)
    {
        if (popup is ListBox<string> list)
            SelectKeys.AttachTo(list);

        return SelectStyle.Default.PopupTemplateFactory?.Invoke(popup) ?? popup;
    }
}
