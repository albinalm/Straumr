using System.Text;
using XenoAtom.Ansi;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Models;

internal sealed class StraumrStyleSetModel
{
    private readonly TextBlockStyle _delete;

    private readonly TextBlockStyle _get;
    private readonly TextBlockStyle _otherMethod;
    private readonly TextBlockStyle _patch;
    private readonly TextBlockStyle _post;
    private readonly TextBlockStyle _put;

    private readonly bool _selectionInverts;
    private readonly bool _rampBlends;

    private static readonly string[] FlowerFrames = ["·", "✢", "✳", "✶", "✻", "✽", "✽", "✻", "✶", "✳", "✢", "·"];
    private static readonly Rune BlankRune = new(' ');
    private static readonly LineGlyphs BlankGlyphs = new(BlankRune, BlankRune, BlankRune, BlankRune, BlankRune, BlankRune,
        BlankRune, BlankRune, BlankRune, BlankRune, BlankRune);

    public StraumrStyleSetModel(StraumrPaletteModel palette, StraumrChromeModel chrome)
    {
        Palette = palette;
        Chrome = chrome;
        _selectionInverts = palette.SelectionInverts;
        _rampBlends = chrome.Ramp.All(color => color.IsRgbLike);
        bool modern = chrome.Layout == StraumrLayoutKind.V2;
        FrameworkTheme = palette.Background.Kind == ColorKind.Default
            ? new Theme
            {
                Foreground = Text,
                Background = Background,
                Surface = Background,
                SurfaceAlt = Raised,
                PopupSurface = Background,
                ControlFill = Background,
                ControlFillHover = Hover,
                ControlFillPressed = SelectionInactive,
                InputFill = Background,
                InputFillFocused = Background,
                Border = Border,
                FocusBorder = Accent,
                Accent = Accent,
                Selection = Selection,
                Disabled = Muted,
                Primary = Accent,
                Success = Green,
                Warning = Amber,
                Error = Red,
                Muted = Muted
            }
            : Theme.Default;

        PrimaryText = TextBlockStyle.Default with
        {
            Foreground = Text,
            TextStyle = TextStyle.None
        };

        MutedText = PrimaryText with { Foreground = Muted, TextStyle = MutedDecoration(Muted) };
        BrightText = PrimaryText with { Foreground = TextBright };
        MutedBrightText = PrimaryText with { Foreground = MutedBright };
        AccentText = PrimaryText with { Foreground = Accent };
        GreenText = PrimaryText with { Foreground = Green };

        FocusChip = palette.SelectionInverts
            ? PrimaryText with { Foreground = Text, Background = Background, TextStyle = TextStyle.Invert, FillBackground = true }
            : PrimaryText with { Foreground = TextBright, Background = Selection, FillBackground = true };

        TokenChip = PrimaryText with
        {
            Foreground = MutedBright,
            Background = Raised,
            TextStyle = Raised.Kind == ColorKind.Default ? TextStyle.Bold : TextStyle.None,
            FillBackground = true
        };

        AmberText = PrimaryText with { Foreground = Amber };
        RedText = PrimaryText with { Foreground = Red };
        RedBrightText = PrimaryText with { Foreground = RedBright };
        PurpleText = PrimaryText with { Foreground = Purple };
        BrandText = PrimaryText with { Foreground = Brand };
        SectionText = PrimaryText with { Foreground = palette.Section, TextStyle = TextStyle.Bold };
        FocusedSectionText = PrimaryText with { Foreground = Accent, TextStyle = TextStyle.Bold };
        IdleSectionText = PrimaryText with { Foreground = MutedBright, TextStyle = TextStyle.Bold };

        MethodPaletteModel methods = palette.Methods;
        _get = PrimaryText with { Foreground = methods.Get };
        _post = PrimaryText with { Foreground = methods.Post };
        _put = PrimaryText with { Foreground = methods.Put };
        _patch = PrimaryText with { Foreground = methods.Patch };
        _delete = PrimaryText with { Foreground = methods.Delete };
        _otherMethod = PrimaryText with { Foreground = methods.Other };

        CodePaletteModel code = palette.Code;
        CodeKey = Style.None.WithForeground(code.Key);
        CodeString = Style.None.WithForeground(code.String);
        CodeNumber = Style.None.WithForeground(code.Number);
        CodeBoolean = Style.None.WithForeground(code.Boolean);
        CodeNull = MutedSurface(code.Null);
        CodePunctuation = MutedSurface(code.Punctuation);
        CodePlain = Style.None.WithForeground(Text);
        CodeNote = MutedSurface(Muted);
        CodeKeyText = PrimaryText with { Foreground = code.Key };
        CodePunctuationText = PrimaryText with { Foreground = code.Punctuation, TextStyle = MutedDecoration(code.Punctuation) };

        CommandBar = CommandBarStyle.Default with
        {
            Background = Background,
            Foreground = Muted,
            KeyForeground = Accent,
            KeyBackground = Background,
            Separator = modern ? "  " : "   ",
            KeycapOpen = new Rune(' '),
            KeycapClose = new Rune(' ')
        };

        PreviewTabs = TabControlStyle.NoBorder with
        {
            TabPadding = new Thickness(1, 0, 1, 0),
            StripStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            TabStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            TabHoveredStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            TabPressedStyle = Style.None.WithForeground(Accent).WithBackground(Background),
            TabSelectedStyle = Style.None.WithForeground(Accent).WithBackground(Background),
            BorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            FocusedBorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            OverflowButtonStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            OverflowButtonHoveredStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            OverflowButtonPressedStyle = Style.None.WithForeground(Accent).WithBackground(Background)
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
            Focused = SelectedSurface(TextBright) | TextStyle.Bold,
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

        RuleTab = Button with
        {
            Normal = Style.None.WithForeground(Muted).WithBackground(Background),
            Hovered = Style.None.WithForeground(Muted).WithBackground(Background),
            Pressed = Style.None.WithForeground(Accent).WithBackground(Background),
            Focused = Style.None.WithForeground(Muted).WithBackground(Background),
            Disabled = Style.None.WithForeground(Muted).WithBackground(Background)
        };

        RuleTabSelected = RuleTab with
        {
            Normal = Style.None.WithForeground(Accent).WithBackground(Background),
            Hovered = Style.None.WithForeground(Accent).WithBackground(Background),
            Pressed = Style.None.WithForeground(Accent).WithBackground(Background),
            Focused = Style.None.WithForeground(Accent).WithBackground(Background)
        };

        RuleTabFocused = RuleTab with
        {
            Normal = SelectedSurface(TextBright) | TextStyle.Bold,
            Hovered = SelectedSurface(TextBright) | TextStyle.Bold,
            Pressed = SelectedSurface(TextBright) | TextStyle.Bold,
            Focused = SelectedSurface(TextBright) | TextStyle.Bold
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

        SelectPopup = (modern ? BorderStyle.Rounded : BorderStyle.Single) with
        {
            BorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            FocusedBorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            BackgroundStyle = Style.None.WithBackground(Background)
        };

        SelectList = ListBoxStyle.Default with
        {
            Item = Style.None.WithForeground(TextBright).WithBackground(Background),
            SelectedFocused = SelectedSurface(TextBright),
            SelectedUnfocused = Style.None.WithForeground(Text).WithBackground(SelectionInactive),
            Disabled = Style.None.WithForeground(Muted).WithBackground(Background)
        };

        MenuPopup = (modern ? GroupStyle.Rounded : GroupStyle.Single) with
        {
            BorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            FocusedBorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            BackgroundStyle = Style.None.WithBackground(Background)
        };

        Menu = MenuListStyle.Default with
        {
            ItemStyle = Style.None.WithBackground(Background),
            SelectedStyle = SelectedSurface(TextBright),
            HoveredStyle = Style.None.WithForeground(TextBright).WithBackground(Hover),
            DisabledStyle = Style.None.WithForeground(Muted).WithBackground(Background),
            SeparatorStyle = Style.None.WithForeground(Border).WithBackground(Background),
            PopupTemplateFactory = OpenedMenu
        };

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
            ThumbOff = SwitchThumb(MutedBright),
            ThumbOn = SwitchThumb(Green)
        };

        Validation = ValidationStyle.Default with
        {
            Padding = new Thickness(0),
            ErrorGlyph = null,
            ErrorStyle = Style.None.WithForeground(Red).WithBackground(Background)
        };

        Dialog = (modern ? DialogStyle.Rounded : DialogStyle.Single) with
        {
            SurfaceStyle = Style.None.WithForeground(Text).WithBackground(Background).WithTextStyle(TextStyle.None),
            BorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            FocusedBorderCellStyle = Style.None.WithForeground(Border).WithBackground(Background),
            LabelBackgroundStyle = Style.None.WithBackground(Background)
        };

        ScreenDialog = modern ? Dialog with { Glyphs = BlankGlyphs } : Dialog;

        WindowGroup = GroupStyle.Single with
        {
            Glyphs = modern ? BlankGlyphs : GroupStyle.Single.Glyphs,
            BorderCellStyle = Style.None.WithForeground(Border),
            FocusedBorderCellStyle = Style.None.WithForeground(Border),
            LabelBackgroundStyle = Style.None.WithBackground(Background),
            BackgroundStyle = Style.None.WithBackground(Background)
        };

        RequestPulse = !chrome.Motion.Spinner ? Still("RequestPulse", "●") : new SpinnerStyle("RequestPulse", TimeSpan.FromMilliseconds(80), "●···········", "·●··········", "··●·········", "···●········", "····●·······", "·····●······",
                "······●·····", "·······●····", "········●···", "·········●··", "··········●·", "···········●", "··········●·", "·········●··", "········●···", "·······●····",
                "······●·····", "·····●······", "····●·······", "···●········", "··●·········", "·●··········")
            { Foreground = Accent };

        LoadingSpinner = !chrome.Motion.Spinner
            ? Still("Loading", modern ? "✻" : "●")
            : modern
                ? new SpinnerStyle("Flower", TimeSpan.FromMilliseconds(110), FlowerFrames) { Foreground = Accent, TextStyle = TextStyle.Bold }
                : SpinnerStyle.Default;

        Flower = FlowerFrames;

        PromptFrame = BorderStyle.Rounded with
        {
            BorderCellStyle = Style.None.WithForeground(Accent).WithBackground(Background),
            FocusedBorderCellStyle = Style.None.WithForeground(Accent).WithBackground(Background),
            BackgroundStyle = Style.None.WithBackground(Background)
        };

        AccentDivider = RuleStyle.Default with
        {
            LineStyle = Style.None.WithForeground(Accent)
        };

        GaugeTrack = MutedSurface(ScrollTrack);

        ListScrollViewer = ScrollViewerStyle.Default with
        {
            TrackStyle = MutedSurface(ScrollTrack).WithBackground(Background),
            ThumbStyle = Style.None.WithForeground(ScrollThumb).WithBackground(Background)
        };

        Divider = RuleStyle.Default with
        {
            LineStyle = MutedSurface(Border)
        };

        DividerCell = MutedSurface(Border);

        ScrollTrackCell = MutedSurface(ScrollTrack)
            .WithBackground(Background);

        ScrollThumbCell = Style.None
            .WithForeground(ScrollThumb)
            .WithBackground(Background);

        SelectedItem = SelectedSurface(TextBright);

        SelectedItemInactive = Style.None
            .WithForeground(Text)
            .WithBackground(SelectionInactive);

        SelectionMarker = _selectionInverts
            ? Style.None.WithForeground(Accent).WithBackground(Background).WithTextStyle(TextStyle.Bold)
            : SelectedSurface(Accent);

        SelectionMarkerInactive = _selectionInverts
            ? Style.None.WithForeground(Accent).WithBackground(Background).WithTextStyle(TextStyle.None)
            : Style.None.WithForeground(Muted).WithBackground(SelectionInactive);

        HoveredItem = Style.None
            .WithForeground(Text)
            .WithBackground(Hover);
    }
    public StraumrPaletteModel Palette { get; }

    public StraumrChromeModel Chrome { get; }

    public bool IsModern => Chrome.Layout == StraumrLayoutKind.V2;

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
    public TextBlockStyle SectionText { get; }
    public TextBlockStyle FocusedSectionText { get; }
    public TextBlockStyle IdleSectionText { get; }

    public Style CodeKey { get; }
    public Style CodeString { get; }
    public Style CodeNumber { get; }
    public Style CodeBoolean { get; }
    public Style CodeNull { get; }
    public Style CodePunctuation { get; }
    public Style CodePlain { get; }
    public Style CodeNote { get; }
    public TextBlockStyle CodeKeyText { get; }
    public TextBlockStyle CodePunctuationText { get; }
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
    public BorderStyle SelectPopup { get; }
    public ListBoxStyle SelectList { get; }
    public MenuListStyle Menu { get; }
    public GroupStyle MenuPopup { get; }
    public SwitchStyle Switch { get; }
    public ValidationStyle Validation { get; }
    public DialogStyle Dialog { get; }
    public DialogStyle ScreenDialog { get; }
    public GroupStyle WindowGroup { get; }
    public SpinnerStyle RequestPulse { get; }
    public SpinnerStyle LoadingSpinner { get; }
    public IReadOnlyList<string> Flower { get; }
    public BorderStyle PromptFrame { get; }
    public RuleStyle AccentDivider { get; }
    public Style GaugeTrack { get; }
    public ScrollViewerStyle ListScrollViewer { get; }
    public RuleStyle Divider { get; }
    public Style DividerCell { get; }
    public Style ScrollTrackCell { get; }
    public Style ScrollThumbCell { get; }
    public Style SelectedItem { get; }
    public Style SelectedItemInactive { get; }
    public Style SelectionMarker { get; }
    public Style SelectionMarkerOnBand => _selectionInverts
        ? Style.None.WithForeground(Text).WithBackground(Accent).WithTextStyle(TextStyle.Invert)
        : SelectionMarker;
    public Style SelectionMarkerInactive { get; }
    public Style HoveredItem { get; }

    public Color RampColor(double t)
    {
        IReadOnlyList<Color> ramp = Chrome.Ramp;
        double position = double.IsFinite(t) ? Math.Clamp(t, 0, 1) : 0;
        if (ramp.Count == 1)
        {
            return ramp[0];
        }

        if (!_rampBlends)
        {
            return ramp[Math.Min(ramp.Count - 1, (int)(position * ramp.Count))];
        }

        double scaled = position * (ramp.Count - 1);
        int index = Math.Min((int)scaled, ramp.Count - 2);
        return Color.Mix(ramp[index], ramp[index + 1], (float)(scaled - index), ColorMixSpace.Oklab);
    }

    public Style Tinted(Color color) => Style.None.WithForeground(color).WithTextStyle(MutedDecoration(color));

    public Color Blend(Color from, Color to, double amount) =>
        from.IsRgbLike && to.IsRgbLike
            ? Color.Mix(from, to, (float)Math.Clamp(amount, 0, 1), ColorMixSpace.Oklab)
            : amount < 0.5 ? from : to;

    private SpinnerStyle Still(string name, string frame) => new(name, TimeSpan.FromHours(1), frame) { Foreground = Accent };

    public TextBlockStyle MethodText(string method) => method.ToUpperInvariant() switch
    {
        "GET" => _get,
        "POST" => _post,
        "PUT" => _put,
        "PATCH" => _patch,
        "DELETE" => _delete,
        _ => _otherMethod
    };

    private Style SelectedSurface(Color foreground) =>
        _selectionInverts
            ? Style.None.WithForeground(Text).WithBackground(Background).WithTextStyle(TextStyle.Invert)
            : Style.None.WithForeground(foreground).WithBackground(Selection);
    public TextBlockStyle SelectedText(TextBlockStyle style) =>
        _selectionInverts
            ? style with { Foreground = Text, Background = Background, TextStyle = TextStyle.Invert }
            : style;
    public Style EditorText(Style style, Style selection) =>
        _selectionInverts && style == selection ? SelectedSurface(TextBright) : style;
    public TextBlockStyle MenuText(TextBlockStyle style) =>
        _selectionInverts ? TextBlockStyle.Default : style;
    private Style SwitchThumb(Color color) =>
        _selectionInverts
            ? Style.None.WithForeground(color).WithBackground(Background).WithTextStyle(TextStyle.None)
            : Style.None.WithForeground(color).WithBackground(Background);
    private static TextStyle MutedDecoration(Color color) =>
        color.Kind == ColorKind.Default ? TextStyle.Dim : TextStyle.None;
    private static Style MutedSurface(Color color) =>
        Style.None.WithForeground(color).WithTextStyle(MutedDecoration(color));

    private static Brush? SolidOrNull(Color color) =>
        color.Kind == ColorKind.Default ? null : Brush.Solid(color);

    public Style CurrentMarker(Color background, bool selected) =>
        selected && _selectionInverts ? SelectedSurface(Accent) : Style.None.WithForeground(Accent).WithBackground(background);

    public string KeyMarkup(string text) => $"[{MarkupToken(Accent)}][bold]{AnsiMarkup.Escape(text)}[/][/]";

    private static string MarkupToken(Color color) => color.Kind switch
    {
        ColorKind.Default => "default",
        ColorKind.Basic16 or ColorKind.Indexed256 => color.Index.ToString(),
        _ => color.ToHexString()
    };

    private Visual? OpenedMenu(Visual list)
    {
        var frame = new Group { Content = list };
        frame.SetStyle(MenuPopup);
        return StraumrSurfaceHelpers.Surface(frame);
    }

    private Visual? OpenedSelect(Visual popup)
    {
        if (popup is ListBox<string> list)
        {
            SelectKeyHelpers.AttachTo(list);
            list.SetStyle(SelectList);
            list.SetStyle(ListScrollViewer);
        }

        Border frame = new Border(popup).Stretch();
        frame.SetStyle(SelectPopup);
        return StraumrSurfaceHelpers.Surface(frame);
    }
}
