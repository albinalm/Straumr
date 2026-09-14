using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Visuals.Shared;

internal sealed class PreviewPane
{
    private readonly State<string>[] _text;
    private readonly ScrollableContent[] _views;
    private readonly State<int> _page = new(0);
    private readonly TabControl? _tabs;

    public PreviewPane(params string[] pages) : this(false, pages) { }

    public PreviewPane(bool wrap, params string[] pages) : this(wrap, false, pages) { }

    /// <summary>
    /// A pane whose page titles are notched into <see cref="TabRule"/> instead of sitting on a strip
    /// of their own, for a region that is a whole screen rather than one panel of one.
    /// </summary>
    /// <remarks>
    /// A titled rule above a tab strip states the same thing twice and lights two cues at once: the
    /// section's focus chip and the selected tab's accent. On the rule there is one title per page,
    /// the selected one carries the chip while the pane owns focus, and the rule is the screen's tab
    /// strip exactly as it is everywhere else in the app.
    /// </remarks>
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
            for (int index = 0; index < _views.Length; index++)
                _views[index].IsVisible = index == 0;
            Root = new ZStack(_views.Cast<Visual>().ToArray())
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch);
            TabRule = BuildTabRule(pages);
        }
        else
        {
            var tabs = new TabControl().HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);
            // The tab strip is focusable, so it took a Tab of its own inside a region the strip and its
            // content share a title. It stays clickable and stays a pointer focus target; it is only out
            // of the Tab rotation, so one Tab moves between titled regions rather than within one.
            tabs.IsTabStop(false);
            tabs.SetStyle(StraumrStyles.PreviewTabs);
            for (int index = 0; index < pages.Length; index++)
            {
                int pageIndex = index;
                tabs.AddTab(new TextBlock(pages[index]).Style(() => SelectedPage == pageIndex
                    ? StraumrStyles.AccentText : StraumrStyles.MutedText), _views[index]);
            }
            _tabs = tabs;
            Root = tabs;
        }

        // Where the titles are on the rule they are the only titles on it, so nothing else can be
        // stepped to and `Tab` means what it means everywhere else in the app: move the chip along
        // the rule. Where they are on a strip of their own the pane is one titled region among
        // several, `Tab` belongs to those, and `t` moves within this one.
        if (TabRule is not null)
        {
            Root.AddCommand(CycleCommand("NextTab", $"{StraumrStyles.KeyMarkup("/t")} Next tab",
                new KeyGesture(TerminalKey.Tab), CommandPresentation.CommandBar, 1));
            Root.AddCommand(CycleCommand("PreviousTab", "Previous tab",
                new KeyGesture(TerminalKey.Tab, TerminalModifiers.Shift), CommandPresentation.None, -1));
            // The letter that moved pages before `Tab` took the job still does. It cannot carry a
            // keycap of its own — the bar renders one gesture per hint, and a hint with no gesture
            // it renders not at all — so it rides in the presented hint's label instead.
            Root.AddCommand(CycleCommand("NextTabKey", "Next tab", new KeyGesture('t'),
                CommandPresentation.None, 1));
        }
        else
        {
            Root.AddCommand(CycleCommand("NextTab", "Next tab", new KeyGesture('t'),
                CommandPresentation.CommandBar, 1));
        }
    }

    /// <remarks>
    /// Secondary so the hint yields to a screen's own actions on a crowded footer row; the titles
    /// on the rule carry the same meaning where it is cut.
    /// </remarks>
    private Command CycleCommand(string id, string label, KeyGesture gesture,
        CommandPresentation presentation, int step) =>
        new()
        {
            Id = $"PreviewPane.{id}",
            LabelMarkup = label,
            Gesture = gesture,
            Importance = CommandImportance.Secondary,
            Presentation = presentation,
            Execute = _ => Select((SelectedPage + step + _views.Length) % _views.Length)
        };

    public Visual Root { get; }

    /// <summary>The rule carrying the page titles, for a pane built by <see cref="OnRule"/>.</summary>
    public Rule? TabRule { get; }

    public Visual FocusTarget => _views[Math.Clamp(SelectedPage, 0, _views.Length - 1)];

    private int SelectedPage => _tabs?.SelectedIndex ?? _page.Value;

    public Visual Page(int index) => _views[index];

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

    /// <remarks>
    /// Visibility is set outright rather than bound because focus is revoked from a visual that is
    /// invisible during the focus pass: the page being left has to stop being the visible one before
    /// the page being entered is asked for focus, or the pane ends up with no focus at all.
    /// </remarks>
    private void Select(int index)
    {
        if (_tabs is { } tabs)
        {
            tabs.SelectedIndex = index;
            return;
        }

        if (index == _page.Value)
            return;

        bool owned = Root.Owns();
        for (int page = 0; page < _views.Length; page++)
            _views[page].IsVisible = page == index;
        _page.Value = index;
        if (owned)
            FocusTarget.App?.Focus(FocusTarget);
    }

    private Rule BuildTabRule(string[] pages)
    {
        Rule rule = StraumrSurfaces.HorizontalDivider();
        rule.StartLabel(() => new HStack(pages.Select(BuildTab).ToArray()).Spacing(0));
        return rule;
    }

    private Visual BuildTab(string title, int index)
    {
        var tab = new Button(title);
        tab.SetStyle(index == SelectedPage
            ? Root.Owns() ? StraumrStyles.RuleTabFocused : StraumrStyles.RuleTabSelected
            : StraumrStyles.RuleTab);
        // The pane below owns the focus its title reports, so a title is out of the Tab rotation and
        // a click hands focus straight to the page it selected rather than keeping it on the title.
        tab.IsTabStop(false);
        tab.Click(() =>
        {
            Select(index);
            FocusTarget.App?.Focus(FocusTarget);
        });
        return tab;
    }
}
