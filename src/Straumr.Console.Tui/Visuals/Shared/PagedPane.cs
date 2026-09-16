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
    private readonly State<int> _page;

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
        _page = new State<int>(FirstApplicable(0));
        // Visibility is set outright rather than bound because focus is revoked from a visual that is
        // invisible during the focus pass: the page being left has to stop being the visible one
        // before the page being entered is asked for focus.
        for (int index = 0; index < _pages.Length; index++)
            _pages[index].Content.IsVisible = index == SelectedPage;

        Root = new ZStack(_pages.Select(page => page.Content).ToArray())
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        TabRule = StraumrSurfaces.HorizontalDivider();
        TabRule.StartLabel(() => new HStack(_pages
                .Select(BuildTab)
                .OfType<Visual>()
                .ToArray())
            .Spacing(0));

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
        if (index == _page.Value || !_pages[index].Applies)
            return;

        bool owned = Root.Owns();
        for (int page = 0; page < _pages.Length; page++)
            _pages[page].Content.IsVisible = page == index;
        _page.Value = index;
        if (owned)
            Focus();
    }

    /// <summary>
    /// Applies the page-level discriminator: a page that does not apply shows no title and cannot be
    /// stepped to, and the selection moves off it if it is the page currently showing.
    /// </summary>
    /// <remarks>
    /// The same rule a field follows one level down. An auth's pages are decided by its type — a
    /// bearer token has no grant flow and no request of its own — and a page kept on the rule with
    /// nothing on it is indistinguishable from one that failed to load. Assigned rather than bound,
    /// for the reason <c>EditorForm.Sync</c> is: what decides it is the resource being edited, which
    /// is a plain object the binding graph knows nothing about.
    /// </remarks>
    public void Sync() => Select(FirstApplicable(_page.Value));

    /// <summary>
    /// <paramref name="preferred"/> when it applies, and otherwise the first page that does. There is
    /// always one: a resource with no applicable page at all has nothing to edit.
    /// </summary>
    private int FirstApplicable(int preferred)
    {
        if ((uint)preferred < (uint)_pages.Length && _pages[preferred].Applies)
            return preferred;

        int index = Array.FindIndex(_pages, page => page.Applies);
        return index < 0 ? 0 : index;
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
            Execute = _ => Select(Step(step))
        };

    /// <summary>The next page in <paramref name="step"/>'s direction that currently applies.</summary>
    private int Step(int step)
    {
        int index = SelectedPage;
        for (int moved = 0; moved < _pages.Length; moved++)
        {
            index = (index + step + _pages.Length) % _pages.Length;
            if (_pages[index].Applies)
                return index;
        }

        return SelectedPage;
    }

    private Visual? BuildTab(PagedPanePage page, int index)
    {
        if (!page.Applies)
            return null;

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
/// <param name="Visible">
/// Whether the page applies at all given the rest of the resource. Omitted for a page that always
/// does, which is every page a read-only view has.
/// </param>
internal sealed record PagedPanePage(string Title, Visual Content, Func<Visual> FocusTarget)
{
    public Func<bool>? Visible { get; init; }

    public bool Applies => Visible?.Invoke() ?? true;
}
