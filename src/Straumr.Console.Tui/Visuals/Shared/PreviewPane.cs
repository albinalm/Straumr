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
            for (int index = 0; index < pages.Length; index++)
            {
                int pageIndex = index;
                tabs.AddTab(new TextBlock(pages[index]).Style(() => SelectedPage == pageIndex
                    ? StraumrStyles.AccentText : StraumrStyles.MutedText), _views[index]);
            }
            _tabs = tabs;
            Root = tabs;
            // Where the titles are on a strip of their own the pane is one titled region among
            // several, `Tab` belongs to those, and `t` moves within this one.
            Root.AddCommand(new Command
            {
                Id = "PreviewPane.NextTab",
                LabelMarkup = "Next tab",
                Gesture = new KeyGesture('t'),
                Importance = CommandImportance.Secondary,
                Presentation = CommandPresentation.CommandBar,
                Execute = _ => tabs.SelectedIndex = (tabs.SelectedIndex + 1) % _views.Length
            });
        }
    }

    public Visual Root { get; }

    /// <summary>The rule carrying the page titles, for a pane built by <see cref="OnRule"/>.</summary>
    public Rule? TabRule { get; }

    public Visual FocusTarget => _views[Math.Clamp(SelectedPage, 0, _views.Length - 1)];

    private int SelectedPage => _tabs?.SelectedIndex ?? _paged!.SelectedPage;

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
}
