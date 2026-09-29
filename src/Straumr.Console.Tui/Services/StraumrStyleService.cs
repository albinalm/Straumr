using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Services;

internal static class StraumrStyleService
{
    private static StraumrStyleSetModel _set = Build(StraumrThemeService.DefaultReference);

    public static string ThemeName { get; private set; } = "Terminal";

    public static Theme FrameworkTheme => _set.FrameworkTheme;

    public static Color Background => _set.Background;
    public static Color Raised => _set.Raised;
    public static Color Selection => _set.Selection;
    public static Color SelectionInactive => _set.SelectionInactive;
    public static Color Hover => _set.Hover;
    public static Color Border => _set.Border;
    public static Color ScrollTrack => _set.ScrollTrack;
    public static Color ScrollThumb => _set.ScrollThumb;
    public static Color Text => _set.Text;
    public static Color TextBright => _set.TextBright;
    public static Color Muted => _set.Muted;
    public static Color MutedBright => _set.MutedBright;
    public static Color Accent => _set.Accent;
    public static Color Amber => _set.Amber;
    public static Color Green => _set.Green;
    public static Color Red => _set.Red;
    public static Color RedBright => _set.RedBright;
    public static Color Purple => _set.Purple;
    public static Color Brand => _set.Brand;

    public static TextBlockStyle PrimaryText => _set.PrimaryText;
    public static TextBlockStyle MutedText => _set.MutedText;
    public static TextBlockStyle BrightText => _set.BrightText;
    public static TextBlockStyle MutedBrightText => _set.MutedBrightText;
    public static TextBlockStyle AccentText => _set.AccentText;
    public static TextBlockStyle GreenText => _set.GreenText;
    public static TextBlockStyle FocusChip => _set.FocusChip;
    public static TextBlockStyle TokenChip => _set.TokenChip;
    public static TextBlockStyle AmberText => _set.AmberText;
    public static TextBlockStyle RedText => _set.RedText;
    public static TextBlockStyle RedBrightText => _set.RedBrightText;
    public static TextBlockStyle PurpleText => _set.PurpleText;
    public static TextBlockStyle BrandText => _set.BrandText;

    public static Style CodeKey => _set.CodeKey;
    public static Style CodeString => _set.CodeString;
    public static Style CodeNumber => _set.CodeNumber;
    public static Style CodeBoolean => _set.CodeBoolean;
    public static Style CodeNull => _set.CodeNull;
    public static Style CodePunctuation => _set.CodePunctuation;
    public static Style CodePlain => _set.CodePlain;
    public static Style CodeNote => _set.CodeNote;
    public static TextBlockStyle CodeKeyText => _set.CodeKeyText;
    public static TextBlockStyle CodePunctuationText => _set.CodePunctuationText;

    public static CommandBarStyle CommandBar => _set.CommandBar;
    public static TabControlStyle PreviewTabs => _set.PreviewTabs;
    public static Style CommandPromptText => _set.CommandPromptText;
    public static PromptEditorStyle CommandPrompt => _set.CommandPrompt;
    public static ButtonStyle Button => _set.Button;
    public static ButtonStyle DangerButton => _set.DangerButton;
    public static ButtonStyle PrimaryButton => _set.PrimaryButton;
    public static ButtonStyle RuleTab => _set.RuleTab;
    public static ButtonStyle RuleTabSelected => _set.RuleTabSelected;
    public static ButtonStyle RuleTabFocused => _set.RuleTabFocused;
    public static TextBoxStyle TextBox => _set.TextBox;
    public static SelectStyle Select => _set.Select;
    public static MenuListStyle Menu => _set.Menu;
    public static SwitchStyle Switch => _set.Switch;
    public static ValidationStyle Validation => _set.Validation;
    public static DialogStyle Dialog => _set.Dialog;
    public static GroupStyle WindowGroup => _set.WindowGroup;
    public static SpinnerStyle RequestPulse => _set.RequestPulse;
    public static ScrollViewerStyle ListScrollViewer => _set.ListScrollViewer;
    public static BorderStyle SuggestionPopup => _set.SelectPopup;
    public static RuleStyle Divider => _set.Divider;

    public static Style DividerCell => _set.DividerCell;
    public static Style ScrollTrackCell => _set.ScrollTrackCell;
    public static Style ScrollThumbCell => _set.ScrollThumbCell;
    public static Style SelectedItem => _set.SelectedItem;
    public static Style SelectedItemInactive => _set.SelectedItemInactive;
    public static Style SelectionMarker => _set.SelectionMarker;
    public static Style SelectionMarkerInactive => _set.SelectionMarkerInactive;
    public static Style HoveredItem => _set.HoveredItem;

    public static void Apply(StraumrThemeModel theme)
    {
        _set = new StraumrStyleSetModel(theme.Palette);
        ThemeName = theme.Name;
    }

    private static StraumrStyleSetModel Build(string reference)
    {
        StraumrThemeService.TryResolve(reference, string.Empty, out StraumrThemeModel theme, out _);
        return new StraumrStyleSetModel(theme.Palette);
    }

    public static TextBlockStyle MethodText(string method) => _set.MethodText(method);

    public static Style CurrentMarker(Color background, bool selected) => _set.CurrentMarker(background, selected);
    public static TextBlockStyle SelectedText(TextBlockStyle style) => _set.SelectedText(style);
    public static Style EditorText(Style style, Style selection) => _set.EditorText(style, selection);
    public static TextBlockStyle MenuText(TextBlockStyle style) => _set.MenuText(style);
    public static void ClearTerminalSurface(CellBuffer buffer, Rectangle bounds)
    {
        if (Background.Kind != ColorKind.Default)
        {
            return;
        }
        buffer.PushClip(bounds);
        try
        {
            // Default colours inherit underlying cells in XenoAtom 3.9.0; clearing replaces those cells instead.
            buffer.ClearCurrentClip(Style.None.WithForeground(Text).WithBackground(Background).WithTextStyle(TextStyle.None));
        }
        finally
        {
            buffer.PopClip();
        }
    }

    public static string KeyMarkup(string text) => _set.KeyMarkup(text);
}
