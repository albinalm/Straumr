using System.Text;
using XenoAtom.Ansi;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Layout;
using XenoAtom.Terminal.UI.Rendering;
using XenoAtom.Terminal.UI.Styling;
using XenoAtom.Terminal.UI.Text;

namespace Straumr.Console.Tui.Screens.Components.Shared;

public sealed partial class HintBar : Visual
{
    private static readonly Rune Blank = new(' ');
    private readonly HashSet<string> _dedup = new(StringComparer.Ordinal);
    private readonly List<HintBarItemModel> _global = [];
    private readonly List<HintBarItemModel> _hints = [];
    private readonly List<HintBarItemModel> _local = [];

    private readonly MarkupTextParser _parser = new();
    private readonly Dictionary<KeySequence, string> _sequenceText = [];
    private int _moreRow;
    private int _moreX = -1;

    public HintBar()
    {
        HorizontalAlignment = Align.Stretch;
    }

    [Bindable]
    public partial string? HoveredCommandId { get; set; }

    public int MaxRows { get; init; } = int.MaxValue;

    public Func<string?>? MoreKey { get; init; }

    public string MoreLabel { get; init; } = "more";

    protected override SizeHints MeasureCore(in LayoutConstraints constraints)
    {
        var style = GetStyle<CommandBarStyle>();
        Collect();

        int available = constraints.IsWidthBounded ? Math.Max(1, constraints.MaxWidth) : int.MaxValue;
        (int width, int rows) = Layout(style, available);
        Size size = constraints.Clamp(new Size(width, rows));

        return SizeHints.Flex(new Size(0, 1), size, size, 0, 0, 1, 0);
    }

    protected override void RenderOverride(CellBuffer buffer)
    {
        Rectangle bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var style = GetStyle<CommandBarStyle>();
        CommandBarResolvedStyle styles = style.Resolve(GetTheme());

        for (int y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (int x = bounds.X; x < bounds.Right; x++)
            {
                buffer.SetCell(x, y, Blank, styles.BarStyle);
            }
        }

        Collect();
        Layout(style, Math.Max(1, bounds.Width));

        foreach (HintBarItemModel hint in _hints)
        {
            if (hint.Row < 0)
            {
                continue;
            }

            int y = bounds.Y + hint.Row;
            if (y >= bounds.Bottom)
            {
                break;
            }

            if (hint.SeparatorWidth > 0)
            {
                Write(
                    buffer, bounds, bounds.X + hint.X - hint.SeparatorWidth, y,
                    style.Separator, styles.LabelStyle);
            }

            bool underline = hint.IsEnabled && hint.Command.Id == HoveredCommandId;
            Style keyStyle = Decorate(styles.KeyStyle, underline);
            Style labelStyle = Decorate(
                hint.IsEnabled ? styles.LabelStyle : styles.DisabledLabelStyle, underline);

            int cursor = bounds.X + hint.X;
            if (cursor >= bounds.Right)
            {
                continue;
            }

            buffer.SetCell(cursor++, y, style.KeycapOpen, keyStyle);
            cursor = Write(buffer, bounds, cursor, y, hint.KeyText, keyStyle);
            if (cursor < bounds.Right)
            {
                buffer.SetCell(cursor++, y, style.KeycapClose, keyStyle);
            }

            cursor = Write(buffer, bounds, cursor, y, " ", labelStyle);
            WriteLabel(buffer, bounds, cursor, y, hint, labelStyle, underline);
        }

        if (_moreX >= 0 && MoreKey?.Invoke() is { Length: > 0 } more && bounds.Y + _moreRow < bounds.Bottom)
        {
            int y = bounds.Y + _moreRow;
            int cursor = Write(buffer, bounds, bounds.X + _moreX, y, style.Separator, styles.LabelStyle);
            buffer.SetCell(cursor++, y, style.KeycapOpen, styles.KeyStyle);
            cursor = Write(buffer, bounds, cursor, y, more, styles.KeyStyle);
            if (cursor < bounds.Right)
            {
                buffer.SetCell(cursor++, y, style.KeycapClose, styles.KeyStyle);
            }

            Write(buffer, bounds, cursor, y, " " + MoreLabel, styles.DisabledLabelStyle);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e) =>
        HoveredCommandId = HintAt(e.UiX, e.UiY)?.Command.Id;

    protected override void OnHoveredChanged(bool isHovered)
    {
        if (!isHovered)
        {
            HoveredCommandId = null;
        }
    }

    protected override void OnPointerPressed(PointerEventArgs e)
    {
        if (e.Button != TerminalMouseButton.Left || e.ClickCount != 1)
        {
            return;
        }

        if (HintAt(e.UiX, e.UiY) is not { } hint || !hint.Command.CanExecuteFor(hint.Target))
        {
            return;
        }

        e.Handled = true;
        hint.Command.Execute(hint.Target);
    }

    private HintBarItemModel? HintAt(int uiX, int uiY)
    {
        Rectangle bounds = Bounds;
        int row = uiY - bounds.Y;
        int column = uiX - bounds.X;

        foreach (HintBarItemModel hint in _hints)
        {
            if (hint.Row < 0 || hint.Row != row || column < hint.X || column >= hint.X + hint.Width)
            {
                continue;
            }

            return hint.IsEnabled ? hint : null;
        }

        return null;
    }

    private static Style Decorate(Style style, bool underline) =>
        underline ? style.AddTextStyle(TextStyle.Underline) : style;

    private void Collect()
    {
        _hints.Clear();
        _local.Clear();
        _global.Clear();
        _dedup.Clear();

        if (App is not { } app)
        {
            return;
        }

        Dictionary<string, AnsiStyle> markupStyles = GetTheme().GetMarkupStyles();
        Visual? focused = app.FocusedElement;

        for (Visual? visual = focused; visual is not null; visual = visual.Parent)
        {
            Append(_local, visual, visual.Commands, markupStyles);
            if (visual is IModalVisual { IsModal: true })
            {
                _local.Sort(ByImportance);
                _hints.AddRange(_local);
                return;
            }
        }

        if (app.GlobalCommands.Count > 0)
        {
            Append(_global, focused ?? app.Root, app.GlobalCommands, markupStyles);
        }

        _local.Sort(ByImportance);
        _global.Sort(ByImportance);
        _hints.AddRange(_local);
        _hints.AddRange(_global);
    }

    private static int ByImportance(HintBarItemModel a, HintBarItemModel b) =>
        a.Command.Importance.CompareTo(b.Command.Importance);

    private void Append(
        List<HintBarItemModel> destination,
        Visual target,
        IReadOnlyList<Command> commands,
        Dictionary<string, AnsiStyle> markupStyles)
    {
        foreach (Command command in commands)
        {
            if ((command.Presentation & CommandPresentation.CommandBar) == CommandPresentation.None)
            {
                continue;
            }

            if (command.Gesture is null && command.Sequence is null)
            {
                continue;
            }

            if (!command.IsVisibleFor(target) || !_dedup.Add(command.Id))
            {
                continue;
            }

            string keyText = KeyText(command);
            if (keyText.Length == 0)
            {
                continue;
            }

            string labelText = _parser.Parse(command.LabelMarkup, out StyledRun[] runs, markupStyles);

            destination.Add(new HintBarItemModel
            {
                Command = command,
                Target = target,
                IsEnabled = command.CanExecuteFor(target),
                KeyText = keyText,
                LabelText = labelText,
                LabelRuns = runs,
                Width = TerminalTextUtility.GetWidth(keyText.AsSpan()) + 3 +
                        TerminalTextUtility.GetWidth(labelText.AsSpan())
            });
        }
    }

    private string KeyText(Command command)
    {
        if (command.Sequence is { } sequence)
        {
            if (!_sequenceText.TryGetValue(sequence, out string? text))
            {
                _sequenceText[sequence] = text = sequence.ToString();
            }

            return text;
        }

        if (command.Gesture is { } gesture)
        {
            return TuiKeybindHelpers.Text(gesture);
        }

        return string.Empty;
    }

    private (int Width, int Rows) Layout(CommandBarStyle style, int availableWidth)
    {
        int separatorWidth = TerminalTextUtility.GetWidth((style.Separator ?? string.Empty).AsSpan());
        _moreX = -1;
        (int width, int rows, _) = Place(separatorWidth, availableWidth, int.MaxValue, 0);
        if (rows <= MaxRows)
        {
            return (width, rows);
        }

        string more = MoreKey?.Invoke() ?? string.Empty;
        int reserve = more.Length == 0 ? 0 : separatorWidth + more.Length + 3 + MoreLabel.Length;
        (width, rows, int end) = Place(separatorWidth, availableWidth, MaxRows, reserve);
        if (more.Length > 0)
        {
            _moreRow = rows - 1;
            _moreX = end;
            width = Math.Max(width, end + reserve);
        }

        return (width, rows);
    }

    private (int Width, int Rows, int End) Place(int separatorWidth, int availableWidth, int maxRows, int reserve)
    {
        int x = 0;
        int row = 0;
        int widest = 0;
        bool hasEntry = false;
        bool full = false;

        foreach (HintBarItemModel hint in _hints)
        {
            int separator = hasEntry ? separatorWidth : 0;
            int limit = row == maxRows - 1 ? availableWidth - reserve : availableWidth;
            if (!full && x > 0 && x + separator + hint.Width > limit)
            {
                if (row + 1 >= maxRows)
                {
                    full = true;
                }
                else
                {
                    row++;
                    x = 0;
                    separator = 0;
                    limit = row == maxRows - 1 ? availableWidth - reserve : availableWidth;
                }
            }

            if (full || x + separator + hint.Width > limit && x > 0)
            {
                full = true;
                hint.Row = -1;
                continue;
            }

            hint.SeparatorWidth = separator;
            hint.Row = row;
            hint.X = x + separator;
            x += separator + hint.Width;
            widest = Math.Max(widest, x);
            hasEntry = true;
        }

        return (widest, row + 1, x);
    }

    private static int Write(
        CellBuffer buffer, Rectangle rect, int x, int y, ReadOnlySpan<char> text, Style style)
    {
        if (y >= rect.Bottom)
        {
            return x;
        }

        foreach (char character in text)
        {
            if (x >= rect.Right)
            {
                break;
            }

            buffer.SetCell(x++, y, new Rune(character), style);
        }

        return x;
    }

    private static void WriteLabel(
        CellBuffer buffer, Rectangle rect, int x, int y, HintBarItemModel hint, Style baseStyle, bool underline)
    {
        if (hint.LabelRuns.Length == 0)
        {
            Write(buffer, rect, x, y, hint.LabelText, baseStyle);
            return;
        }

        foreach (StyledRun run in hint.LabelRuns)
        {
            if (x >= rect.Right)
            {
                break;
            }

            Style style = Decorate(baseStyle | run.Style, underline);
            int end = Math.Min(run.Start + run.Length, hint.LabelText.Length);
            for (int index = run.Start; index < end && x < rect.Right; index++)
            {
                buffer.SetCell(x++, y, new Rune(hint.LabelText[index]), style);
            }
        }
    }
}
