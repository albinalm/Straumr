using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class SecretSuggestions
{
    private const int MaxRows = 8;

    private const int BorderRows = 2;

    private const int FooterRows = 2;

    private readonly TextBox _input;
    private readonly State<int> _capacity = new(MaxRows);
    private readonly State<string[]> _matches = new([]);
    private readonly State<int> _offset = new(0);
    private readonly State<bool> _open = new(false);
    private readonly State<int> _selected = new(0);
    private bool _isRewriting;
    private SecretTokenModel _token;
    private int _visibleRows = MaxRows;

    public SecretSuggestions(TextBox input)
    {
        _input = input;

        var frame = new Border(new ComputedVisual(Build));
        frame.SetStyle(StraumrStyleService.SuggestionPopup);
        Root = frame
            .HorizontalAlignment(Align.Start)
            .IsVisible(() => _open.Value && _input.HasFocus);

        AddCommand("SecretSuggestions.Next", "Choose", CommandPresentation.CommandBar, () => Move(1));
        AddCommand("SecretSuggestions.Previous", "Previous secret", CommandPresentation.None, () => Move(-1));
        AddCommand("SecretSuggestions.Insert", "Insert secret", CommandPresentation.CommandBar, Insert);
        AddCommand("SecretSuggestions.Dismiss", "Dismiss", CommandPresentation.None, () => _open.Value = false);
    }

    public Visual Root { get; }

    public void Sync()
    {
        if (_isRewriting)
        {
            return;
        }

        string text = _input.Text ?? string.Empty;
        if (SecretTokenHelpers.At(text, _input.CaretIndex) is not { } token)
        {
            _open.Value = false;
            return;
        }

        string[] matches = [.. SecretCatalogService.Match(SecretTokenHelpers.Prefix(text, token))];
        if (matches.Length == 0)
        {
            _open.Value = false;
            return;
        }

        _token = token;
        if (!_matches.Value.AsSpan().SequenceEqual(matches))
        {
            _matches.Value = matches;
            _selected.Value = 0;
            _offset.Value = 0;
        }

        _capacity.Value = Capacity();
        _open.Value = true;
    }

    private static int Window(int offset, int selected, int count, int rows) =>
        Math.Clamp(
            offset,
            Math.Max(0, selected - rows + 1),
            Math.Max(0, Math.Min(selected, count - rows)));

    private void AddCommand(string id, string label, CommandPresentation presentation, Action execute) =>
        _input.AddCommand(new Command
        {
            Id = id,
            LabelMarkup = label,
            Gesture = TuiKeybindHelpers.Get(id),
            Importance = CommandImportance.Primary,
            Presentation = presentation,
            CanExecute = _ => _open.Value,
            IsVisible = _ => _open.Value,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => execute()
        });

    private int Capacity()
    {
        Rectangle input = _input.Bounds;
        if (input.Height <= 0 || _input.App is not { } app)
        {
            return MaxRows;
        }

        int limit = app.Root.Bounds.Bottom - FooterRows;
        for (Visual? node = _input.Parent; node is not null; node = node.Parent)
        {
            if (node is not (ScrollViewer or FlexiblePane) || node.Bounds.Height <= 0)
            {
                continue;
            }

            int inset = node is FlexiblePane ? ResourceScreenLayoutHelpers.PaneInset.Bottom : 0;
            limit = Math.Min(limit, node.Bounds.Bottom - inset);
            break;
        }

        return Math.Clamp(limit - input.Bottom - BorderRows, 1, MaxRows);
    }

    private Visual Build()
    {
        string[] names = _matches.Value;
        if (names.Length == 0)
        {
            return new TextBlock(string.Empty);
        }

        int capacity = _capacity.Value;
        bool counted = names.Length > capacity && capacity >= 2;
        int rows = Math.Min(names.Length, counted ? capacity - 1 : capacity);
        _visibleRows = rows;

        int offset = Window(_offset.Value, _selected.Value, names.Length, rows);
        List<Visual> lines = new(rows + 1);
        for (int index = 0; index < rows; index++)
        {
            int row = offset + index;
            lines.Add(new TextBlock($" {names[row]} ")
                .Style(() => row == _selected.Value
                    ? StraumrStyleService.FocusChip
                    : StraumrStyleService.BrightText)
                .Trimming(TextTrimming.EndEllipsis)
                .HorizontalAlignment(Align.Stretch));
        }

        if (counted)
        {
            lines.Add(new TextBlock(() => $" {_selected.Value + 1}/{names.Length} ")
                .Style(StraumrStyleService.MutedText));
        }

        return new VStack(lines.ToArray());
    }

    private void Move(int step)
    {
        string[] names = _matches.Value;
        if (names.Length == 0)
        {
            return;
        }

        int next = (_selected.Value + step + names.Length) % names.Length;
        _selected.Value = next;
        _offset.Value = Window(_offset.Value, next, names.Length, _visibleRows);
    }

    private void Insert()
    {
        string[] names = _matches.Value;
        if (names.Length == 0)
        {
            return;
        }

        string name = names[Math.Clamp(_selected.Value, 0, names.Length - 1)];

        _isRewriting = true;
        try
        {
            _input.TextDocument.Replace(
                _token.NameStart, _token.ReplaceLength, SecretTokenHelpers.Replacement(_token, name));
            _input.CaretIndex = SecretTokenHelpers.CaretAfter(_token, name);
        }
        finally
        {
            _isRewriting = false;
        }

        _open.Value = false;
    }
}
