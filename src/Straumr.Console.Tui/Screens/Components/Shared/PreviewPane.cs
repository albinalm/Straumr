using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class PreviewPane
{
    private readonly Func<Visual>[] _focus;
    private readonly State<Func<string, StyledRun[]>?>[] _highlighters;
    private readonly PagedPane _paged;
    private readonly ScrollableContent?[] _scrollers;
    private readonly State<string>[] _text;
    private readonly Visual[] _views;

    public PreviewPane(params PreviewPanePageModel[] pages) : this(false, false, pages) { }

    private PreviewPane(bool wrap, bool tabsOnRule, PreviewPanePageModel[] pages)
    {
        _text = pages.Select(_ => new State<string>(string.Empty)).ToArray();
        _highlighters = pages.Select(_ => new State<Func<string, StyledRun[]>?>(null)).ToArray();
        _views = new Visual[pages.Length];
        _scrollers = new ScrollableContent?[pages.Length];
        _focus = new Func<Visual>[pages.Length];
        for (int page = 0; page < pages.Length; page++)
        {
            if (pages[page].Content is { } custom)
            {
                Func<Visual> target = pages[page].FocusTarget ?? (() => custom);
                _views[page] = custom;
                _focus[page] = target;
                continue;
            }

            State<string> text = _text[page];
            State<Func<string, StyledRun[]>?> highlighter = _highlighters[page];
            var scroller = new ScrollableContent(
                new ComputedVisual(() => Lines(text.Value, highlighter.Value, wrap)), !tabsOnRule);
            _scrollers[page] = scroller;
            _views[page] = scroller;
            _focus[page] = () => scroller;
        }

        _paged = new PagedPane(
            pages.Select((page, index) =>
                new PagedPanePageModel(page.Title, _views[index], _focus[index])).ToArray());
        if (tabsOnRule)
        {
            Root = _paged.Root;
            TabRule = _paged.TabRule;
        }
        else
        {
            Root = new Grid()
                .Columns(new ColumnDefinition { Width = GridLength.Star() })
                .Rows(new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star() })
                .Cell(_paged.TabRule, 0, 0)
                .Cell(_paged.Root, 1, 0)
                .HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);
        }
    }

    public Visual Root { get; }

    public Rule? TabRule { get; }

    public Visual FocusTarget => _focus[Math.Clamp(SelectedPage, 0, _focus.Length - 1)]();

    private int SelectedPage => _paged.SelectedPage;

    public static PreviewPane OnRule(bool wrap, params PreviewPanePageModel[] pages) => new(wrap, true, pages);

    public Visual Page(int index) => _views[index];

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
        if (_scrollers[index] is not { } scroller || _text[index].Value == value)
        {
            return;
        }

        _text[index].Value = value;
        scroller.ScrollOffset = 0;
    }
}
