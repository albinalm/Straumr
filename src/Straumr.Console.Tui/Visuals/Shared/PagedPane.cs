using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared;

/// <summary>
/// A region whose pages are its whole content, with their titles notched into the rule above it
/// instead of sitting on a tab strip of their own. It is the rule-as-tab-strip idiom by itself,
/// knowing nothing about what a page contains: <see cref="PreviewPane"/> fills its pages with
/// scrollable text and the resource editor fills them with forms.
/// </summary>
/// <remarks>
/// A titled rule above a tab strip states the same thing twice and lights two cues at once. On the
/// rule there is one title per page, the selected one carries the focus chip while the pane owns
/// focus, and the rule is the screen's tab strip exactly as it is everywhere else in the app.
/// </remarks>
internal sealed class PagedPane
{
    private readonly PagedPanePage[] _pages;
    private readonly State<int> _page = new(0);

    /// <summary>The C0 control character a terminal sends for <c>Ctrl</c> plus a letter.</summary>
    private const char CycleLetter = 't';

    /// <param name="tabCyclesPages">
    /// Whether the pages hold anything focusable of their own. True for read-only pages: the titles
    /// are then the only thing on the rule that can be stepped to, <c>Tab</c> keeps meaning "move the
    /// chip along the rule", and a bare <c>t</c> can sit beside it. False where a page is a form,
    /// which changes both keys: <c>Tab</c> belongs to the fields, and the page gesture has to stop
    /// being a printable character.
    /// </param>
    public PagedPane(bool tabCyclesPages, params PagedPanePage[] pages)
    {
        _pages = pages;
        // Visibility is set outright rather than bound because focus is revoked from a visual that is
        // invisible during the focus pass: the page being left has to stop being the visible one
        // before the page being entered is asked for focus.
        for (int index = 0; index < _pages.Length; index++)
            _pages[index].Content.IsVisible = index == 0;

        Root = new ZStack(_pages.Select(page => page.Content).ToArray())
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        TabRule = StraumrSurfaces.HorizontalDivider();
        TabRule.StartLabel(() => new HStack(_pages.Select(BuildTab).ToArray()).Spacing(0));

        if (tabCyclesPages)
        {
            Root.AddCommand(CycleCommand("NextTab", $"{StraumrStyles.KeyMarkup("/t")} Next tab",
                new KeyGesture(TerminalKey.Tab), CommandPresentation.CommandBar, 1));
            Root.AddCommand(CycleCommand("PreviousTab", "Previous tab",
                new KeyGesture(TerminalKey.Tab, TerminalModifiers.Shift), CommandPresentation.None, -1));
            // The letter that moved pages before `Tab` took the job still does. It cannot carry a
            // keycap of its own — the bar renders one gesture per hint, and a hint with no gesture it
            // renders not at all — so it rides in the presented hint's label instead.
            Root.AddCommand(CycleCommand("NextTabKey", "Next tab", new KeyGesture(CycleLetter),
                CommandPresentation.None, 1));
        }
        else
        {
            // A bare letter here would be an ancestor of every field on every page, and commands are
            // collected up the focus chain, so it fired while a name was being typed into one. The
            // gesture therefore carries Ctrl, as the control character a terminal actually sends; the
            // letter-and-modifier form is registered beside it, unpresented, for a host that reports
            // the two separately.
            Root.AddCommand(CycleCommand("NextPage", "Next page",
                new KeyGesture((char)(char.ToUpperInvariant(CycleLetter) & 0x1F), TerminalModifiers.Ctrl),
                CommandPresentation.CommandBar, 1));
            Root.AddCommand(CycleCommand("NextPageLetter", "Next page",
                new KeyGesture(CycleLetter, TerminalModifiers.Ctrl),
                CommandPresentation.None, 1));
        }
    }

    public Visual Root { get; }

    /// <summary>The rule carrying the page titles. The caller places it directly above the pane.</summary>
    public Rule TabRule { get; }

    public int SelectedPage => _page.Value;

    public Visual FocusTarget => _pages[Math.Clamp(SelectedPage, 0, _pages.Length - 1)].FocusTarget();

    public Visual Page(int index) => _pages[index].Content;

    public void Select(int index)
    {
        if (index == _page.Value)
            return;

        bool owned = Root.Owns();
        for (int page = 0; page < _pages.Length; page++)
            _pages[page].Content.IsVisible = page == index;
        _page.Value = index;
        if (owned)
            Focus();
    }

    public void Focus()
    {
        Visual target = FocusTarget;
        target.App?.Focus(target);
    }

    /// <remarks>
    /// Secondary so the hint yields to a screen's own actions on a crowded footer row; the titles on
    /// the rule carry the same meaning where it is cut.
    /// </remarks>
    private Command CycleCommand(string id, string label, KeyGesture gesture,
        CommandPresentation presentation, int step) =>
        new()
        {
            Id = $"PagedPane.{id}",
            LabelMarkup = label,
            Gesture = gesture,
            Importance = CommandImportance.Secondary,
            Presentation = presentation,
            Execute = _ => Select((SelectedPage + step + _pages.Length) % _pages.Length)
        };

    private Visual BuildTab(PagedPanePage page, int index)
    {
        var tab = new Button(page.Title);
        tab.SetStyle(index == SelectedPage
            ? Root.Owns() ? StraumrStyles.RuleTabFocused : StraumrStyles.RuleTabSelected
            : StraumrStyles.RuleTab);
        // The pane below owns the focus its title reports, so a title is out of the Tab rotation and
        // a click hands focus straight to the page it selected rather than keeping it on the title.
        tab.IsTabStop(false);
        tab.Click(() =>
        {
            Select(index);
            Focus();
        });
        return tab;
    }
}

/// <param name="FocusTarget">
/// Asked for rather than held, because a page whose content is rebuilt — a form whose first visible
/// field depends on another field's value — has no one visual that is permanently its entry point.
/// </param>
internal sealed record PagedPanePage(string Title, Visual Content, Func<Visual> FocusTarget);
