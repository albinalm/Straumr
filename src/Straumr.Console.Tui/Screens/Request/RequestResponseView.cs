using System.Diagnostics;
using System.Text;
using Straumr.Console.Shared.Models;
using Straumr.Console.Tui.Formatting;
using Straumr.Console.Tui.Visuals.Shared;
using Straumr.Core.Enums;
using Straumr.Core.Models;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Input;

namespace Straumr.Console.Tui.Screens.Request;

/// <summary>
/// The full-screen response view: a screen of the app rather than a dialog over one. It is built
/// from the pieces the shell is built from — the identity header, a three-row bar closed by the rule
/// that carries the titles, a pane, and the one-row footer — so the extra room it takes is spent on
/// the body and the measurements rather than on chrome of its own.
/// </summary>
internal sealed class RequestResponseView
{
    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(5);

    private readonly Dialog _dialog;
    private readonly PreviewPane _preview = PreviewPane.OnRule(true, "Body", "Headers", "Network");
    private readonly ResponseBodyActions _body;
    private readonly State<bool> _sending = new(true);
    private readonly State<bool> _failed = new(false);
    private readonly State<string> _summary = new(string.Empty);
    private readonly State<string> _notice = new(string.Empty);
    private readonly State<bool> _noticeError = new(false);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private DateTimeOffset _noticeUntil;
    private bool _focused;

    public RequestResponseView(StraumrRequest request, string? workspaceName,
        Func<ResponseBodyFormat> arrivalFormat, Action cancel, Action resend, Action closed)
    {
        _body = new ResponseBodyActions(_preview, (message, error) =>
        {
            _notice.Value = message;
            _noticeError.Value = error;
            _noticeUntil = DateTimeOffset.UtcNow + NoticeLifetime;
        }, arrivalFormat, bounded: false);
        _preview.SetText("Waiting for the response body…", "Waiting for response headers…",
            "Network measurements will appear when the request completes.");

        var content = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(StraumrHeader.Create(() => SecretFormatting.Display(request.Name), () => workspaceName), 0, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 1, 0)
            .Cell(BuildBar(request), 2, 0)
            .Cell(_preview.TabRule!, 3, 0)
            .Cell(ResourceScreenLayout.Pane(_preview.Root), 4, 0)
            .Cell(StraumrSurfaces.HorizontalDivider(), 5, 0)
            .Cell(BuildFooter(), 6, 0)
            .HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);

        _dialog = StraumrDialog.CreateScreen(content);
        AddExit("Cancel", () => _sending.Value, () =>
        {
            _summary.Value = "Cancelling…";
            cancel();
        });
        AddExit("Back", () => !_sending.Value, () => { _dialog.Close(); closed(); });
        _dialog.AddCommand(new Command
        {
            Id = "Response.Send", LabelMarkup = "Send again", Gesture = new KeyGesture('s'),
            Importance = CommandImportance.Primary, Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => !_sending.Value, IsVisible = _ => !_sending.Value,
            ConsumesGestureWhenUnavailable = false, Execute = _ => resend()
        });
    }

    public void Show()
    {
        _dialog.Show();
        FocusBody();
    }

    public void Update()
    {
        FocusBody();
        if (_notice.Value.Length > 0 && DateTimeOffset.UtcNow >= _noticeUntil)
            _notice.Value = string.Empty;
    }

    /// <summary>Puts the view back in flight for a second send of the same request.</summary>
    public void Restart()
    {
        _clock.Restart();
        _sending.Value = true;
        _failed.Value = false;
        _summary.Value = string.Empty;
        _body.SetBody(null);
        _preview.SetText("Waiting for the response body…", "Waiting for response headers…",
            "Network measurements will appear when the request completes.");
    }

    /// <remarks>
    /// A screen opens with a region focused, and the body is the region this one is about. It is
    /// asked for once the dialog has an app to ask — <c>AutoFocus</c> is not enough here, because the
    /// dialog takes the focus pass that follows its own <c>Show</c>.
    /// </remarks>
    private void FocusBody()
    {
        if (_focused || _preview.FocusTarget.App is not { } app)
            return;
        _focused = true;
        app.Focus(_preview.FocusTarget);
    }

    public void Complete(StraumrResponse response, bool cached = false)
    {
        _clock.Stop();
        _sending.Value = false;
        _failed.Value = response.Exception is not null || (int?)response.StatusCode >= 400;
        long bytes = response.RawContent?.LongLength ?? Encoding.UTF8.GetByteCount(response.Content ?? string.Empty);
        string status = response.StatusCode is { } code ? $"{(int)code} {response.ReasonPhrase}" : "Send failed";
        string protocol = response.HttpVersion?.ToString() ?? "unavailable";
        _summary.Value = $"{status} · {response.Duration.TotalMilliseconds:0.##} ms · {ContentFormatting.Size(bytes)} · HTTP {protocol}";

        var measurements = new List<KeyValuePair<string, string>>
        {
            new("Status", status),
            new("HTTP version", protocol),
            new("HTTP elapsed", $"{response.Duration.TotalMilliseconds:0.##} ms"),
            new("Response body", $"{bytes:N0} bytes ({ContentFormatting.Size(bytes)})")
        };
        if (!cached)
            measurements.Add(new("Total elapsed", $"{_clock.Elapsed.TotalMilliseconds:0.##} ms (including preparation)"));
        if (response.TimeToHeaders is { } headers)
            measurements.Add(new("Time to headers", $"{headers.TotalMilliseconds:0.##} ms"));
        if (response.BodyDownloadDuration is { } download)
        {
            measurements.Add(new("Body download", $"{download.TotalMilliseconds:0.##} ms"));
            if (download.TotalSeconds > 0 && bytes > 0)
                measurements.Add(new("Average read rate", $"{ContentFormatting.Size((long)(bytes / download.TotalSeconds))}/s"));
        }
        measurements.Add(new("Response headers", response.ResponseHeaders.Count.ToString()));

        var network = new StringBuilder(ContentFormatting.Fields(measurements, "No measurements."));
        if (response.Warnings.Count > 0)
            network.Append("\n\nWarnings\n").AppendJoin('\n', response.Warnings);
        if (response.Exception is { } exception)
            network.Append("\n\n").Append(exception.Message);

        _preview.SetText(response.Exception?.Message ?? "No body.",
            ContentFormatting.Headers(response.ResponseHeaders), network.ToString());
        _body.SetBody(response.Content);
        if (response.Exception is not null)
            _preview.SetPageText(0, response.Exception.Message);
    }

    /// <param name="summary">
    /// What the bar reports, kept short: the bar's other half is the URL, and a transport error's
    /// whole sentence would leave nothing of it. The sentence goes to <paramref name="detail"/> and
    /// is read in full on the pages below.
    /// </param>
    public void Fail(string summary, string? detail = null)
    {
        _clock.Stop();
        _sending.Value = false;
        _failed.Value = true;
        _summary.Value = summary;
        _body.SetBody(null);
        string message = detail is null ? summary : $"{summary}: {detail}";
        string measurements = ContentFormatting.Fields(
            [new("Status", summary), new("Elapsed", $"{_clock.Elapsed.TotalMilliseconds:0.##} ms")],
            "No measurements.");
        _preview.SetText(message, "No response headers.", $"{measurements}\n\n{message}");
    }

    /// <remarks>
    /// The bar keeps its three rows whether the request is in flight or finished, so completing a
    /// send swaps what the right half says instead of collapsing a row and shifting the screen under
    /// the reader. Only the half being shown is built, so the in-flight label reserves no width in
    /// the finished bar and the measurements reserve none in the in-flight one.
    /// </remarks>
    private Visual BuildBar(StraumrRequest request)
    {
        Visual pulse = InFlightPulse.Create("IN FLIGHT", _clock);
        Visual summary = new TextBlock(() => _summary.Value)
            .Style(() => _failed.Value ? StraumrStyles.RedText : StraumrStyles.MutedBrightText)
            .Trimming(TextTrimming.EndEllipsis);

        Visual bar = StraumrSurfaces.Bar(
            new HStack(
                    // The method names what was sent and is four characters at most, so it keeps its
                    // width and the URL beside it is what gives way.
                    new TextBlock(request.Method.Method).Style(HttpMethodFormatting.Style(request.Method))
                        .MinWidth(request.Method.Method.Length),
                    new TextBlock(RequestEditorState.FromRequest(request).GetDisplayUri())
                        .Style(StraumrStyles.PrimaryText)
                        .Trimming(TextTrimming.EndEllipsis)
                        .HorizontalAlignment(Align.Stretch))
                .Spacing(2)
                .HorizontalAlignment(Align.Stretch),
            new ComputedVisual(() => _sending.Value ? pulse : summary));

        return StraumrSurfaces.Inset(bar, ResourceScreenLayout.PaneInset);
    }

    /// <remarks>
    /// One row holding one thing at a time, as the shell's footer does: the shortcut hints, or a
    /// command's result until it expires.
    /// </remarks>
    private Visual BuildFooter()
    {
        // Wrapped rather than clipped; see StraumrTuiApp's footer.
        var hints = new CommandBar { MultiLine = true }.Style(StraumrStyles.CommandBar);
        return new ZStack(
                StraumrSurfaces.Inset(hints, StraumrSurfaces.RowInset)
                    .IsVisible(() => _notice.Value.Length == 0),
                StraumrSurfaces.Inset(
                        new TextBlock(() => _notice.Value)
                            .Style(() => _noticeError.Value ? StraumrStyles.RedText : StraumrStyles.MutedText)
                            .Trimming(TextTrimming.EndEllipsis)
                            .HorizontalAlignment(Align.Stretch),
                        StraumrSurfaces.RowInset)
                    .IsVisible(() => _notice.Value.Length > 0))
            .HorizontalAlignment(Align.Stretch);
    }

    private void AddExit(string label, Func<bool> available, Action execute) => _dialog.AddCommand(new Command
    {
        Id = $"Response.{label}", LabelMarkup = label, Gesture = new KeyGesture(TerminalKey.Escape),
        Importance = CommandImportance.Primary, Presentation = CommandPresentation.CommandBar,
        CanExecute = _ => available(), IsVisible = _ => available(),
        ConsumesGestureWhenUnavailable = false, Execute = _ => execute()
    });
}
