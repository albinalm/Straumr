using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared;

internal sealed class PreviewPane
{
    private readonly State<string>[] _text;
    private readonly ScrollableContent[] _views;
    private readonly TabControl? _tabs;
    private readonly PagedPane? _paged;

    public PreviewPane(params string[] pages) : this(false, pages) { }

    public PreviewPane(bool wrap, params string[] pages) : this(wrap, false, pages) { }

    /// <summary>
    /// A pane whose page titles are notched into <see cref="TabRule"/> instead of sitting on a strip
    /// of their own, for a region that is a whole screen rather than one panel of one.
    /// </summary>
    public static PreviewPane OnRule(bool wrap, params string[] pages) => new(wrap, true, pages);

    private PreviewPane(bool wrap, bool tabsOnRule, string[] pages)
    {
        _text = pages.Select(_ => new State<string>(string.Empty)).ToArray();
        // A pane that is a whole screen shares the footer with that screen's actions, so it leaves
        // the movement keys unadvertised rather than spending half the row on them; they still work.
        _views = _text.Select(text => new ScrollableContent(new ComputedVisual(() =>
            new VStack(text.Value.Replace("\r", string.Empty).Split('\n').Select(line =>
                new TextBlock(line.Length == 0 ? " " : line)
                    .Style(StraumrStyles.PrimaryText)
                    .Wrap(wrap)
                    .Trimming(TextTrimming.EndEllipsis)
                    .HorizontalAlignment(Align.Stretch)).ToArray())
                .HorizontalAlignment(Align.Stretch)), hints: !tabsOnRule)).ToArray();

        if (tabsOnRule)
        {
            // Where the titles are on the rule they are the only titles on it, so nothing else can
            // be stepped to and `Tab` means what it means everywhere else in the app: move the chip
            // along the rule. A read-only page holds nothing focusable of its own to compete for it.
            _paged = new PagedPane(tabCyclesPages: true,
                pages.Select((title, index) =>
                    new PagedPanePage(title, _views[index], () => _views[index])).ToArray());
            Root = _paged.Root;
            TabRule = _paged.TabRule;
        }
        else
        {
            var tabs = new TabControl().HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);
            // The tab strip is focusable, so it took a Tab of its own inside a region the strip and its
            // content share a title. It stays clickable and stays a pointer focus target; it is only out
            // of the Tab rotation, so one Tab moves between titled regions rather than within one.
            tabs.IsTabStop(false);
            tabs.SetStyle(StraumrStyles.PreviewTabs);
            // Every tab is given the same content: one stack holding all the pages, with the current
            // one made visible outright. The control's own way is to host the selected page alone and
            // swap it, which detaches the page focus is on and leaves focus nowhere until the next
            // page is attached a pass later — long enough for the app to re-home focus outside the
            // pane and for the footer to lose this page's keys, both of which are visible. Handing it
            // one content visual that never changes takes the swap out of the control's hands; it
            // keeps the strip, the selection and the styling, and the pages stop coming and going.
            // It is also what `PagedPane` has always done, which is why it never had this problem.
            // One instance, given to every tab: assigning the content host the visual it already
            // holds is a no-op, so nothing is detached when the selection moves.
            ZStack stack = new ZStack(_views.Cast<Visual>().ToArray())
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch);
            for (int index = 0; index < pages.Length; index++)
            {
                int pageIndex = index;
                tabs.AddTab(new TextBlock(pages[index]).Style(() => SelectedPage == pageIndex
                    ? StraumrStyles.AccentText : StraumrStyles.MutedText), stack);
            }
            _tabs = tabs;
            Root = tabs;
            // A page is selected by the key below and by a click on the strip, so what follows a
            // selection belongs to the selection and not to either of the ways of making one.
            tabs.SelectionChanged(ShowSelectedPage);
            ShowSelectedPage();
            // Where the titles are on a strip of their own the pane is one titled region among
            // several, `Tab` belongs to those, and `t` moves within this one.
            Root.AddCommand(new Command
            {
                Id = "PreviewPane.NextTab",
                LabelMarkup = "Next tab",
                Gesture = new KeyGesture('t'),
                Importance = CommandImportance.Secondary,
                Presentation = CommandPresentation.CommandBar,
                Execute = _ => SelectNextTab()
            });
        }
    }

    public Visual Root { get; }

    /// <summary>The rule carrying the page titles, for a pane built by <see cref="OnRule"/>.</summary>
    public Rule? TabRule { get; }

    public Visual FocusTarget => _views[Math.Clamp(SelectedPage, 0, _views.Length - 1)];

    private int SelectedPage => _tabs?.SelectedIndex ?? _paged!.SelectedPage;

    public Visual Page(int index) => _views[index];

    private void SelectNextTab() =>
        _tabs!.SelectedIndex = (_tabs.SelectedIndex + 1) % _views.Length;

    /// <summary>
    /// Shows the selected page, hides the rest, and takes focus with it when the pane had it.
    /// </summary>
    /// <remarks>
    /// Visibility is assigned rather than bound, and focus is asked for after it: focus is revoked
    /// from a visual that is not visible when the focus pass runs, so the page being left has to
    /// stop being the visible one before the page being entered is asked for it. Every page is
    /// attached the whole time, so this is one keystroke's work with no frame in between.
    /// </remarks>
    private void ShowSelectedPage()
    {
        bool owned = Root.Owns();
        for (int index = 0; index < _views.Length; index++)
            _views[index].IsVisible = index == SelectedPage;

        if (!owned)
            return;

        Visual target = FocusTarget;
        target.App?.Focus(target);
    }

    public void SetPageText(int index, string value)
    {
        if (_text[index].Value == value)
            return;
        _text[index].Value = value;
        _views[index].ScrollOffset = 0;
    }

    public void SetText(params string[] pages)
    {
        for (int index = 0; index < _text.Length; index++)
        {
            SetPageText(index, pages[index]);
        }
    }
}
