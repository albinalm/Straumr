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

    public PreviewPane(params string[] pages)
    {
        _text = pages.Select(_ => new State<string>(string.Empty)).ToArray();
        _views = _text.Select(text => new ScrollableContent(new ComputedVisual(() =>
            new VStack(text.Value.Replace("\r", string.Empty).Split('\n').Select(line =>
                new TextBlock(line.Length == 0 ? " " : line)
                    .Style(StraumrStyles.PrimaryText)
                    .Trimming(TextTrimming.EndEllipsis)
                    .HorizontalAlignment(Align.Stretch)).ToArray())
                .HorizontalAlignment(Align.Stretch)))).ToArray();
        Root = new TabControl().HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);
        // The tab strip is focusable, so it took a Tab of its own inside a region the strip and its
        // content share a title. It stays clickable and stays a pointer focus target; it is only out
        // of the Tab rotation, so one Tab moves between titled regions rather than within one.
        Root.IsTabStop(false);
        Root.SetStyle(StraumrStyles.PreviewTabs);
        for (int index = 0; index < pages.Length; index++)
        {
            int pageIndex = index;
            Root.AddTab(new TextBlock(pages[index]).Style(() => Root.SelectedIndex == pageIndex
                ? StraumrStyles.AccentText : StraumrStyles.MutedText), _views[index]);
        }
        Root.AddCommand(new Command
        {
            Id = "PreviewPane.NextTab",
            LabelMarkup = "Next tab",
            Gesture = new KeyGesture('t'),
            Importance = CommandImportance.Secondary,
            Presentation = CommandPresentation.CommandBar,
            Execute = _ =>
            {
                Root.SelectedIndex = (Root.SelectedIndex + 1) % _views.Length;
            }
        });
    }

    public TabControl Root { get; }
    public Visual FocusTarget => _views[Math.Clamp(Root.SelectedIndex, 0, _views.Length - 1)];

    public void SetText(params string[] pages)
    {
        for (int index = 0; index < _text.Length; index++)
        {
            string value = pages[index];
            if (_text[index].Value == value)
                continue;
            _text[index].Value = value;
            _views[index].ScrollOffset = 0;
        }
    }
}
