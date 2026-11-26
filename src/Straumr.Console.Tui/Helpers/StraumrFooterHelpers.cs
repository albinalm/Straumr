using Straumr.Console.Tui.Screens.Components.Shared;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Styling;

namespace Straumr.Console.Tui.Helpers;

internal static class StraumrFooterHelpers
{
    public const string ShortcutsCommandId = "Straumr.Shortcuts";
    private const string LocationGlyph = "⏵⏵";
    private const string Separator = "›";
    private static readonly State<bool> Expanded = new(false);

    public static Visual Create(
        Func<IReadOnlyList<(string Text, TextBlockStyle Style)>> location,
        Func<string?> notice,
        Func<TextBlockStyle> noticeStyle,
        Visual? prompt = null,
        Func<bool>? promptOpen = null,
        string[]? compactHintPriority = null)
    {
        Func<bool> open = promptOpen ?? (() => false);
        Visual noticeText = new TextBlock(() => notice() ?? string.Empty)
            .Style(noticeStyle)
            .Trimming(TextTrimming.EndEllipsis)
            .HorizontalAlignment(Align.Stretch);

        if (!StraumrStyleService.IsModern)
        {
            ZStack classic = new ZStack(
                    StraumrSurfaceHelpers.Inset(new HintBar().Style(StraumrStyleService.CommandBar), StraumrSurfaceHelpers.RowInset)
                        .IsVisible(() => !open() && notice() is null),
                    StraumrSurfaceHelpers.Inset(noticeText, StraumrSurfaceHelpers.RowInset)
                        .IsVisible(() => !open() && notice() is not null))
                .HorizontalAlignment(Align.Stretch);
            if (prompt is not null)
            {
                classic.Add(prompt);
            }

            return classic;
        }

        HintBar full = new HintBar().Style(StraumrStyleService.CommandBar);
        HintBar compact = new HintBar
        {
            MaxRows = 1,
            PreferredCommandIds = compactHintPriority ?? [],
            MoreKey = () => Key(),
            MoreLabel = "more"
        }.Style(StraumrStyleService.CommandBar);

        Visual toast = new HStack(
                new PulseDot(noticeStyle, notice),
                noticeText)
            .Spacing(1)
            .HorizontalAlignment(Align.Stretch);

        Visual right = new ZStack(
                compact.IsVisible(() => !Expanded.Value && notice() is null),
                toast.IsVisible(() => notice() is not null),
                new TextBlock(() => Key() is { } key ? $"{key} hide keys" : string.Empty)
                    .Style(StraumrStyleService.MutedText)
                    .IsVisible(() => Expanded.Value && notice() is null))
            .HorizontalAlignment(Align.Stretch);

        Visual status = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(3)
            .Cell(new ComputedVisual(() => Location(location())), 0, 0)
            .Cell(right, 0, 1)
            .HorizontalAlignment(Align.Stretch);

        VStack modern = new VStack(
                StraumrSurfaceHelpers.Inset(full, StraumrSurfaceHelpers.RowInset)
                    .IsVisible(() => Expanded.Value && !open()))
            .HorizontalAlignment(Align.Stretch);
        if (prompt is not null)
        {
            var frame = new Border(prompt);
            frame.SetStyle(StraumrStyleService.PromptFrame);
            modern.Add(frame.HorizontalAlignment(Align.Stretch).IsVisible(open));
        }

        modern.Add(StraumrSurfaceHelpers.Inset(status, StraumrSurfaceHelpers.RowInset));
        return modern;
    }

    public static Command ShortcutsCommand(Func<bool> available) =>
        new()
        {
            Id = ShortcutsCommandId,
            LabelMarkup = "Keys",
            Gesture = TuiKeybindHelpers.Get(ShortcutsCommandId),
            Importance = CommandImportance.Tertiary,
            Presentation = CommandPresentation.None,
            CanExecute = _ => StraumrStyleService.IsModern && available(),
            IsVisible = _ => StraumrStyleService.IsModern && available(),
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => Expanded.Value = !Expanded.Value
        };

    private static string? Key() =>
        TuiKeybindHelpers.Get(ShortcutsCommandId) is null ? null : TuiKeybindHelpers.Hint(ShortcutsCommandId);

    private static Visual Location(IReadOnlyList<(string Text, TextBlockStyle Style)> parts)
    {
        HStack line = new HStack(new TextBlock(LocationGlyph).Style(StraumrStyleService.AccentText)).Spacing(1);
        for (int index = 0; index < parts.Count; index++)
        {
            if (index > 0)
            {
                line.Add(new TextBlock(Separator).Style(StraumrStyleService.MutedText));
            }

            line.Add(new TextBlock(parts[index].Text).Style(parts[index].Style).Trimming(TextTrimming.EndEllipsis));
        }

        return line;
    }
}
