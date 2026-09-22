using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

internal sealed class SecretSuggestions
{
    private const int MaxRows = 8;

    private readonly TextBox _input;
    private readonly State<string[]> _matches = new([]);
    private readonly State<int> _offset = new(0);
    private readonly State<bool> _open = new(false);
    private readonly State<int> _selected = new(0);
    private bool _isRewriting;
    private SecretTokenModel _token;

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

        _open.Value = true;
    }

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

    private Visual Build()
    {
        string[] names = _matches.Value;
        if (names.Length == 0)
        {
            return new TextBlock(string.Empty);
        }

        int offset = Math.Clamp(_offset.Value, 0, Math.Max(0, names.Length - 1));
        int rows = Math.Min(MaxRows, names.Length - offset);
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

        if (names.Length > rows)
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
        _offset.Value = Math.Clamp(
            _offset.Value,
            Math.Max(0, next - MaxRows + 1),
            Math.Max(0, Math.Min(next, names.Length - MaxRows)));
    }

    private void Insert()
    {
        string[] names = _matches.Value;
        if (names.Length == 0)
        {
            return;
        }

        string name = names[Math.Clamp(_selected.Value, 0, names.Length - 1)];
        string rewritten = SecretTokenHelpers.Complete(_input.Text ?? string.Empty, _token, name, out int caret);

        _isRewriting = true;
        try
        {
            _input.Text = rewritten;
            _input.CaretIndex = caret;
        }
        finally
        {
            _isRewriting = false;
        }

        _open.Value = false;
    }
}
