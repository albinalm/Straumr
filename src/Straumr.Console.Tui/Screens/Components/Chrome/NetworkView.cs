using Straumr.Core.Models;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;
using ScrollableContent = Straumr.Console.Tui.Screens.Components.Shared.ScrollableContent;

namespace Straumr.Console.Tui.Screens.Components.Chrome;

internal sealed class NetworkView
{
    private const int LabelWidth = 10;
    private const int ValueWidth = 12;
    private static readonly Thickness RowInset = new(2, 0, 0, 0);
    private readonly State<Visual> _content;
    private readonly ScrollableContent _scroller;

    public NetworkView(string message)
    {
        _content = new State<Visual>(Message(message));
        _scroller = new ScrollableContent(new ComputedVisual(() => _content.Value).HorizontalAlignment(Align.Stretch));
    }

    public Visual Root => _scroller;

    public void SetMessage(string text, bool error = false) => Show(Message(text, error));

    public void SetResponse(StraumrResponse response, TimeSpan? elapsed = null) => Show(Build(response, elapsed));

    public void SetFailure(string summary, string? detail, TimeSpan elapsed)
    {
        VStack content = new VStack(
                StraumrSurfaceHelpers.TitledDivider("Timing"),
                Row("elapsed", new GaugeBar(1), Milliseconds(elapsed)),
                Spacer(),
                StraumrSurfaceHelpers.TitledDivider("Failure"),
                Inset(new TextBlock(detail is null ? summary : $"{summary}: {detail}").Style(StraumrStyleService.RedText).Wrap(true)))
            .HorizontalAlignment(Align.Stretch);
        Show(content);
    }

    private void Show(Visual content)
    {
        _content.Value = content;
        _scroller.ScrollOffset = 0;
    }

    private static Visual Build(StraumrResponse response, TimeSpan? elapsed)
    {
        double total = Math.Max(response.Duration.TotalMilliseconds, elapsed?.TotalMilliseconds ?? 0);
        VStack content = new VStack(StraumrSurfaceHelpers.TitledDivider("Timing")).HorizontalAlignment(Align.Stretch);
        if (response.TimeToHeaders is { } headers)
        {
            content.Add(Row("headers", new GaugeBar(Ratio(headers.TotalMilliseconds, total)), Milliseconds(headers)));
        }

        if (response.BodyDownloadDuration is { } download)
        {
            content.Add(Row("body", new GaugeBar(Ratio(download.TotalMilliseconds, total)), Milliseconds(download)));
        }

        content.Add(Row("http", new GaugeBar(Ratio(response.Duration.TotalMilliseconds, total)), Milliseconds(response.Duration)));
        if (elapsed is { } overall)
        {
            content.Add(Row("total", new GaugeBar(Ratio(overall.TotalMilliseconds, total)), Milliseconds(overall)));
        }

        long bytes = response.Bytes;
        content.Add(Spacer());
        content.Add(StraumrSurfaceHelpers.TitledDivider("Transfer"));
        content.Add(Field("status", response.StatusCode is { } code ? $"{(int)code} {response.ReasonPhrase}" : "Send failed",
            ChromeStatus.Style(response)));
        content.Add(Field("size", $"{ContentFormatting.Size(bytes)}  ({bytes:N0} bytes)", StraumrStyleService.PrimaryText));
        if (response.BodyDownloadDuration is { TotalSeconds: > 0 } window && bytes > 0)
        {
            content.Add(Field("rate", $"{ContentFormatting.Size((long)(bytes / window.TotalSeconds))}/s", StraumrStyleService.PrimaryText));
        }

        content.Add(Field("protocol", response.HttpVersion is { } version ? $"HTTP {version}" : "unavailable", StraumrStyleService.PrimaryText));
        if (response.Sent is { } sent)
        {
            content.Add(Field("sent", TimestampFormatting.Relative(sent), StraumrStyleService.PrimaryText));
        }

        content.Add(Field("headers", $"{response.ResponseHeaders.Count} received · {response.RequestHeaders.Count} sent",
            StraumrStyleService.PrimaryText));

        if (response.Warnings.Count > 0)
        {
            content.Add(Spacer());
            content.Add(StraumrSurfaceHelpers.TitledDivider("Warnings"));
            foreach (string warning in response.Warnings)
            {
                content.Add(Inset(new TextBlock($"▲ {warning}").Style(StraumrStyleService.AmberText).Wrap(true)));
            }
        }

        if (response.Exception is { } exception)
        {
            content.Add(Spacer());
            content.Add(StraumrSurfaceHelpers.TitledDivider("Failure"));
            content.Add(Inset(new TextBlock(exception.Message).Style(StraumrStyleService.RedText).Wrap(true)));
        }

        return content;
    }

    private static Visual Message(string text, bool error = false) =>
        new TextBlock(text).Style(error ? StraumrStyleService.RedText : StraumrStyleService.MutedText).Wrap(true)
            .HorizontalAlignment(Align.Stretch);

    private static double Ratio(double part, double total) => total <= 0 ? 0 : part / total;

    private static string Milliseconds(TimeSpan value) => $"{value.TotalMilliseconds:0.#} ms";

    private static Visual Spacer() => new TextBlock(" ");

    private static Visual Inset(Visual content) =>
        new Padder(content).Padding(RowInset).HorizontalAlignment(Align.Stretch);

    private static Visual Row(string label, Visual gauge, string value) =>
        Inset(new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Fixed(LabelWidth) },
                new ColumnDefinition { Width = GridLength.Star() },
                new ColumnDefinition { Width = GridLength.Fixed(ValueWidth) })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(1)
            .Cell(new TextBlock(label).Style(StraumrStyleService.MutedText), 0, 0)
            .Cell(gauge, 0, 1)
            .Cell(new TextBlock(value).Style(StraumrStyleService.MutedBrightText).HorizontalAlignment(Align.End), 0, 2)
            .HorizontalAlignment(Align.Stretch));

    private static Visual Field(string label, string value, TextBlockStyle style) =>
        Inset(new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Fixed(LabelWidth) },
                new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(1)
            .Cell(new TextBlock(label).Style(StraumrStyleService.MutedText), 0, 0)
            .Cell(new TextBlock(value).Style(style).Trimming(TextTrimming.EndEllipsis).HorizontalAlignment(Align.Stretch), 0, 1)
            .HorizontalAlignment(Align.Stretch));
}
