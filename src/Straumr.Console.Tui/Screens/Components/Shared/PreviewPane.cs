using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class PreviewPane
{
    private readonly State<Func<string, StyledRun[]>?>[] _highlighters;
    private readonly PagedPane? _paged;
    private readonly TabControl? _tabs;
    private readonly State<string>[] _text;
    private readonly ScrollableContent[] _views;

    public PreviewPane(params string[] pages) : this(false, pages) { }

    public PreviewPane(bool wrap, params string[] pages) : this(wrap, false, pages) { }

    private PreviewPane(bool wrap, bool tabsOnRule, string[] pages)
    {
        _text = pages.Select(_ => new State<string>(string.Empty)).ToArray();
        _highlighters = pages.Select(_ => new State<Func<string, StyledRun[]>?>(null)).ToArray();
        _views = new ScrollableContent[pages.Length];
        for (int page = 0; page < pages.Length; page++)
        {
            State<string> text = _text[page];
            State<Func<string, StyledRun[]>?> highlighter = _highlighters[page];
            _views[page] = new ScrollableContent(
                new ComputedVisual(() => Lines(text.Value, highlighter.Value, wrap)), !tabsOnRule);
        }

        if (tabsOnRule)
        {
            _paged = new PagedPane(true,
                pages.Select((title, index) =>
                    new PagedPanePageModel(title, _views[index], () => _views[index])).ToArray());
            Root = _paged.Root;
            TabRule = _paged.TabRule;
        }
        else
        {
            TabControl tabs = new TabControl().HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);
            tabs.IsTabStop(false);
            tabs.SetStyle(StraumrStyleService.PreviewTabs);
            ZStack stack = new ZStack(_views.Cast<Visual>().ToArray())
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch);
            for (int index = 0; index < pages.Length; index++)
            {
                int pageIndex = index;
                tabs.AddTab(new TextBlock(pages[index]).Style(() => SelectedPage == pageIndex
                    ? StraumrStyleService.AccentText : StraumrStyleService.MutedText), stack);
            }
            _tabs = tabs;
            Root = tabs;
            tabs.SelectionChanged(ShowSelectedPage);
            ShowSelectedPage();
            Root.AddCommand(new Command
            {
                Id = "PreviewPane.NextTab",
                LabelMarkup = "Next tab",
                Gesture = TuiKeybindHelpers.Get("PreviewPane.NextTab"),
                Importance = CommandImportance.Secondary,
                Presentation = CommandPresentation.CommandBar,
                Execute = _ => SelectNextTab()
            });
        }
    }

    public Visual Root { get; }

    public Rule? TabRule { get; }

    public Visual FocusTarget => _views[Math.Clamp(SelectedPage, 0, _views.Length - 1)];

    private int SelectedPage => _tabs?.SelectedIndex ?? _paged!.SelectedPage;

    public static PreviewPane OnRule(bool wrap, params string[] pages) => new(wrap, true, pages);

    public Visual Page(int index) => _views[index];

    private void SelectNextTab() =>
        _tabs!.SelectedIndex = (_tabs.SelectedIndex + 1) % _views.Length;

    private void ShowSelectedPage()
    {
        bool owned = Root.Owns();
        for (int index = 0; index < _views.Length; index++)
        {
            _views[index].IsVisible = index == SelectedPage;
        }

        if (!owned)
        {
            return;
        }

        Visual target = FocusTarget;
        target.App?.Focus(target);
    }

    private static Visual Lines(string text, Func<string, StyledRun[]>? highlight, bool wrap) =>
        new VStack(text.Replace("\r", string.Empty).Split('\n')
                .Select(line => Line(line.Length == 0 ? " " : line, highlight, wrap))
                .ToArray())
            .HorizontalAlignment(Align.Stretch);

    private static Visual Line(string line, Func<string, StyledRun[]>? highlight, bool wrap) =>
        highlight is null
            ? new TextBlock(line)
                .Style(StraumrStyleService.PrimaryText)
                .Wrap(wrap)
                .Trimming(TextTrimming.EndEllipsis)
                .HorizontalAlignment(Align.Stretch)
            : new Paragraph(line)
                .Runs(highlight(line))
                .Wrap(wrap)
                .Trimming(TextTrimming.EndEllipsis)
                .HorizontalAlignment(Align.Stretch);

    public void SetPageHighlighter(int index, Func<string, StyledRun[]>? highlighter) =>
        _highlighters[index].Value = highlighter;

    public void SetPageText(int index, string value)
    {
        if (_text[index].Value == value)
        {
            return;
        }

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
