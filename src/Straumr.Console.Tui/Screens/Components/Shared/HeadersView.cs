using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class HeadersView
{
    private static readonly Rune SelectionBar = new('▌');
    private readonly ScrollableContent _content;
    private readonly State<string> _empty = new("No headers.");
    private readonly ResourceFilter _filter;
    private readonly State<string[]> _expanded = new([]);
    private readonly State<IReadOnlyList<KeyValuePair<string, string>>> _headers = new([]);
    private readonly Action<string, bool>? _notify;
    private readonly State<string> _query = new(string.Empty);
    private readonly State<int> _selected = new(0);
    private Visual[] _rows = [];

    public HeadersView(Action<string, bool>? notify = null)
    {
        _notify = notify;
        _content = new ScrollableContent(new ComputedVisual(Build), false) { KeyHandler = Navigate };
        _filter = new ResourceFilter("filter headers", Apply, () => _content);
        Root = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star() })
            .Cell(StraumrSurfaceHelpers.Bar(_filter.Root,
                new TextBlock(() => $" {Counts()} ").Style(StraumrStyleService.TokenChip)), 0, 0)
            .Cell(_content, 1, 0)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);
        _filter.AttachCommands(Root, "Filter headers");
        AddHint("ResourceList.Next", "Next", () => Move(1));
        AddHint("ResourceList.Previous", "Prev", () => Move(-1));
        AddHint("ResourceList.First", "First", () => Select(0), CommandImportance.Secondary);
        AddHint("ResourceList.Last", "Last", () => Select(Visible().Count - 1), CommandImportance.Secondary);
        _content.AddCommand(new Command
        {
            Id = "Headers.Expand",
            LabelMarkup = "Expand / collapse",
            Gesture = TuiKeybindHelpers.Get("Headers.Expand"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => Selected() is not null,
            IsVisible = _ => Selected() is not null,
            Execute = _ => ToggleExpand()
        });
        _content.AddCommand(new Command
        {
            Id = "Headers.Copy",
            LabelMarkup = "Copy value",
            Gesture = TuiKeybindHelpers.Get("Headers.Copy"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => Selected() is not null,
            IsVisible = _ => Selected() is not null,
            Execute = _ => Copy()
        });
    }

    public Visual Root { get; }

    public Visual FocusTarget => _content;

    public void SetHeaders(IReadOnlyList<KeyValuePair<string, string>> headers, string empty = "No headers.")
    {
        _empty.Value = empty;
        _headers.Value = headers;
        _expanded.Value = [];
        _selected.Value = 0;
        _content.ScrollOffset = 0;
    }

    public void SetMessage(string message) => SetHeaders([], message);

    private void Apply(string query)
    {
        _query.Value = query.Trim();
        _selected.Value = 0;
        _content.ScrollOffset = 0;
    }

    private string Counts() =>
        _query.Value.Length == 0
            ? _headers.Value.Count.ToString()
            : $"{Visible().Count}/{_headers.Value.Count}";

    private List<KeyValuePair<string, string>> Visible() =>
        _query.Value.Length == 0
            ? _headers.Value.ToList()
            : _headers.Value.Where(header =>
                header.Key.Contains(_query.Value, StringComparison.OrdinalIgnoreCase) ||
                header.Value.Contains(_query.Value, StringComparison.OrdinalIgnoreCase)).ToList();

    private KeyValuePair<string, string>? Selected()
    {
        List<KeyValuePair<string, string>> visible = Visible();
        return (uint)_selected.Value < (uint)visible.Count ? visible[_selected.Value] : null;
    }

    private bool Navigate(KeyEventArgs e)
    {
        int count = Visible().Count;
        if (count == 0)
        {
            return false;
        }

        int pageSize = Math.Max(1, _content.Bounds.Height / 2);
        int? target =
            TuiKeybindHelpers.Matches("ResourceList.Next", e) ? _selected.Value + 1 :
            TuiKeybindHelpers.Matches("ResourceList.Previous", e) ? _selected.Value - 1 :
            TuiKeybindHelpers.Matches("ResourceList.First", e) ? 0 :
            TuiKeybindHelpers.Matches("ResourceList.Last", e) ? count - 1 :
            TuiKeybindHelpers.Matches("ResourceList.Up", e) ? _selected.Value - 1 :
            TuiKeybindHelpers.Matches("ResourceList.Down", e) ? _selected.Value + 1 :
            TuiKeybindHelpers.Matches("ResourceList.Home", e) ? 0 :
            TuiKeybindHelpers.Matches("ResourceList.End", e) ? count - 1 :
            TuiKeybindHelpers.Matches("ResourceList.PageUp", e) ? _selected.Value - pageSize :
            TuiKeybindHelpers.Matches("ResourceList.PageDown", e) ? _selected.Value + pageSize :
            null;

        if (target is null)
        {
            return false;
        }

        Select(target.Value);
        return true;
    }

    private void Move(int delta) => Select(_selected.Value + delta);

    private void Select(int index)
    {
        int count = Visible().Count;
        if (count == 0)
        {
            return;
        }

        _selected.Value = Math.Clamp(index, 0, count - 1);
        Reveal();
    }

    private void Reveal()
    {
        if ((uint)_selected.Value >= (uint)_rows.Length)
        {
            return;
        }

        int top = 0;
        for (int index = 0; index < _selected.Value; index++)
        {
            top += _rows[index].DesiredSize.Height;
        }

        _content.ScrollRangeIntoView(top, _rows[_selected.Value].DesiredSize.Height);
    }

    private void ToggleExpand()
    {
        if (Selected() is not { } header)
        {
            return;
        }

        _expanded.Value = IsExpanded(header.Key)
            ? _expanded.Value.Where(name => !name.Equals(header.Key, StringComparison.OrdinalIgnoreCase)).ToArray()
            : [.. _expanded.Value, header.Key];
        Reveal();
    }

    private bool IsExpanded(string name) =>
        _expanded.Value.Contains(name, StringComparer.OrdinalIgnoreCase);

    private void Copy()
    {
        if (Selected() is not { } header)
        {
            return;
        }

        try
        {
            bool copied = _content.App?.Terminal.Clipboard.TrySetText(header.Value) == true;
            Report(copied ? $"Copied {header.Key}" : "Clipboard is unavailable in this terminal.", !copied);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Report($"Cannot copy header: {exception.Message}", true);
        }
    }

    private void Report(string message, bool failed) => _notify?.Invoke(message, failed);

    private void AddHint(string id, string label, Action execute,
        CommandImportance importance = CommandImportance.Primary) =>
        _content.AddCommand(new Command
        {
            Id = id,
            LabelMarkup = label,
            Gesture = TuiKeybindHelpers.Get(id),
            Importance = importance,
            Presentation = CommandPresentation.CommandBar,
            RouteGesture = false,
            CanExecute = _ => Visible().Count > 0,
            IsVisible = _ => Visible().Count > 0,
            Execute = _ => execute()
        });

    private Visual Build()
    {
        List<KeyValuePair<string, string>> visible = Visible();
        _rows = visible.Select((header, index) => Row(header, index == _selected.Value, IsExpanded(header.Key))).ToArray();
        return _rows.Length == 0
            ? new TextBlock(() => _query.Value.Length == 0 ? _empty.Value : $"No header matches \"{_query.Value}\".")
                .Style(StraumrStyleService.MutedText)
                .Wrap(true)
                .Trimming(TextTrimming.EndEllipsis)
                .HorizontalAlignment(Align.Stretch)
            : new VStack(_rows).HorizontalAlignment(Align.Stretch);
    }

    private Visual Row(KeyValuePair<string, string> header, bool selected, bool expanded)
    {
        string text = $"{header.Key}: {header.Value}";
        Visual line = expanded
            ? new Paragraph(text)
                .Runs([
                    new StyledRun(0, header.Key.Length, StraumrStyleService.CodeKey),
                    new StyledRun(header.Key.Length, 1, StraumrStyleService.CodePunctuation),
                    new StyledRun(header.Key.Length + 1, text.Length - header.Key.Length - 1, StraumrStyleService.CodePlain)
                ])
                .Wrap(true)
                .HorizontalAlignment(Align.Stretch)
            : new HStack(
                    new TextBlock(header.Key).Style(StraumrStyleService.CodeKeyText),
                    new TextBlock(": ").Style(StraumrStyleService.CodePunctuationText),
                    new TextBlock(header.Value)
                        .Style(StraumrStyleService.PrimaryText)
                        .Trimming(TextTrimming.EndEllipsis)
                        .HorizontalAlignment(Align.Stretch))
                .Spacing(0)
                .HorizontalAlignment(Align.Stretch);

        return new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Fixed(2) },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Cell(Marker(selected), 0, 0)
            .Cell(line, 0, 1)
            .HorizontalAlignment(Align.Stretch);
    }

    private Visual Marker(bool selected)
    {
        var canvas = new Canvas(context =>
        {
            if (selected)
            {
                context.DrawVLine(0, 0, context.Size.Height, SelectionBar,
                    _content.HasFocus ? StraumrStyleService.SelectionMarker : StraumrStyleService.SelectionMarkerInactive);
            }
        });
        canvas.HorizontalAlignment = Align.Stretch;
        canvas.VerticalAlignment = Align.Stretch;
        return canvas;
    }
}
