using System.Diagnostics;
using System.Text;
using Straumr.Console.Tui.Screens.Components.Shared;
using Straumr.Core.Models;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;

namespace Straumr.Console.Tui.Screens.Components.Request;

internal sealed class RequestResponseView
{
    private static readonly TimeSpan NoticeLifetime = TimeSpan.FromSeconds(5);
    private readonly ResponseBodyActionService _body;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private readonly Dialog _dialog;
    private readonly State<bool> _failed = new(false);
    private readonly State<string> _notice = new(string.Empty);
    private readonly State<bool> _noticeError = new(false);
    private readonly PreviewPane _preview = PreviewPane.OnRule(true, "Body", "Headers", "Network");
    private readonly State<bool> _sending = new(true);
    private readonly State<string> _summary = new(string.Empty);
    private bool _focused;
    private DateTimeOffset _noticeUntil;

    public RequestResponseView(StraumrRequest request, string? workspaceName,
        Func<ResponseBodyOptionsModel> options, Action cancel, Action resend, Action closed)
    {
        _body = new ResponseBodyActionService(_preview, (message, error) =>
        {
            _notice.Value = message;
            _noticeError.Value = error;
            _noticeUntil = DateTimeOffset.UtcNow + NoticeLifetime;
        }, options, false);
        _preview.SetText("Waiting for the response body…", "Waiting for response headers…",
            "Network measurements will appear when the request completes.");

        Grid content = new Grid()
            .Columns(new ColumnDefinition { Width = GridLength.Star() })
            .Rows(new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star() }, new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Cell(StraumrHeaderHelpers.Create(() => SecretFormatting.Display(request.Name), () => workspaceName), 0, 0)
            .Cell(StraumrSurfaceHelpers.HorizontalDivider(), 1, 0)
            .Cell(BuildBar(request), 2, 0)
            .Cell(_preview.TabRule!, 3, 0)
            .Cell(ResourceScreenLayoutHelpers.Pane(_preview.Root), 4, 0)
            .Cell(StraumrSurfaceHelpers.HorizontalDivider(), 5, 0)
            .Cell(BuildFooter(), 6, 0)
            .HorizontalAlignment(Align.Stretch).VerticalAlignment(Align.Stretch);

        _dialog = StraumrDialogHelpers.CreateScreen(content);
        AddExit("Cancel", () => _sending.Value, () =>
        {
            _summary.Value = "Cancelling…";
            cancel();
        });
        AddExit("Back", () => !_sending.Value, () =>
        {
            _dialog.Close();
            closed();
        });
        _dialog.AddCommand(new Command
        {
            Id = "Response.Send",
            LabelMarkup = "Send again",
            Gesture = TuiKeybindHelpers.Get("Response.Send"),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar,
            CanExecute = _ => !_sending.Value,
            IsVisible = _ => !_sending.Value,
            ConsumesGestureWhenUnavailable = false,
            Execute = _ => resend()
        });
    }

    public void Show() => TuiWindowHelpers.Show(_dialog, FocusBody);

    public void Update()
    {
        FocusBody();
        if (_notice.Value.Length > 0 && DateTimeOffset.UtcNow >= _noticeUntil)
        {
            _notice.Value = string.Empty;
        }
    }

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

    private void FocusBody()
    {
        if (_focused || _preview.FocusTarget.App is not { } app)
        {
            return;
        }

        _focused = true;
        app.Focus(_preview.FocusTarget);
    }

    public void Complete(StraumrResponse response, bool cached = false)
    {
        _clock.Stop();
        _sending.Value = false;
        _failed.Value = response.Exception is not null || (int?)response.StatusCode >= 400;
        long bytes = response.Bytes;
        string status = response.StatusCode is { } code ? $"{(int)code} {response.ReasonPhrase}" : "Send failed";
        string protocol = response.HttpVersion?.ToString() ?? "unavailable";
        _summary.Value = $"{status} · {response.Duration.TotalMilliseconds:0.##} ms · {ContentFormatting.Size(bytes)} · HTTP {protocol}";

        List<KeyValuePair<string, string>> measurements = new()
        {
            new KeyValuePair<string, string>("Status", status),
            new KeyValuePair<string, string>("HTTP version", protocol),
            new KeyValuePair<string, string>("Sent", response.Sent is { } sent ? TimestampFormatting.Relative(sent) : "unavailable"),
            new KeyValuePair<string, string>("HTTP elapsed", $"{response.Duration.TotalMilliseconds:0.##} ms"),
            new KeyValuePair<string, string>("Response body", $"{bytes:N0} bytes ({ContentFormatting.Size(bytes)})")
        };
        if (!cached)
        {
            measurements.Add(new KeyValuePair<string, string>("Total elapsed", $"{_clock.Elapsed.TotalMilliseconds:0.##} ms (including preparation)"));
        }

        if (response.TimeToHeaders is { } headers)
        {
            measurements.Add(new KeyValuePair<string, string>("Time to headers", $"{headers.TotalMilliseconds:0.##} ms"));
        }

        if (response.BodyDownloadDuration is { } download)
        {
            measurements.Add(new KeyValuePair<string, string>("Body download", $"{download.TotalMilliseconds:0.##} ms"));
            if (download.TotalSeconds > 0 && bytes > 0)
            {
                measurements.Add(new KeyValuePair<string, string>("Average read rate", $"{ContentFormatting.Size((long)(bytes / download.TotalSeconds))}/s"));
            }
        }
        measurements.Add(new KeyValuePair<string, string>("Response headers", response.ResponseHeaders.Count.ToString()));

        var network = new StringBuilder(ContentFormatting.Fields(measurements, "No measurements."));
        if (response.Warnings.Count > 0)
        {
            network.Append("\n\nWarnings\n").AppendJoin('\n', response.Warnings);
        }

        if (response.Exception is { } exception)
        {
            network.Append("\n\n").Append(exception.Message);
        }

        _preview.SetText(response.Exception?.Message ?? "No body.",
            ContentFormatting.Headers(response.ResponseHeaders), network.ToString());
        _body.SetBody(response.Content);
        if (response.Exception is not null)
        {
            _preview.SetPageText(0, response.Exception.Message);
        }
        else if (response.BodyOmitted)
        {
            _preview.SetPageText(0, ContentFormatting.Unsaved(bytes, "Response.Send"));
        }
    }

    public void Fail(string summary, string? detail = null)
    {
        _clock.Stop();
        _sending.Value = false;
        _failed.Value = true;
        _summary.Value = summary;
        _body.SetBody(null);
        string message = detail is null ? summary : $"{summary}: {detail}";
        string measurements = ContentFormatting.Fields(
            [new KeyValuePair<string, string>("Status", summary), new KeyValuePair<string, string>("Elapsed", $"{_clock.Elapsed.TotalMilliseconds:0.##} ms")],
            "No measurements.");
        _preview.SetText(message, "No response headers.", $"{measurements}\n\n{message}");
    }

    private Visual BuildBar(StraumrRequest request)
    {
        Visual pulse = InFlightPulseHelpers.Create("IN FLIGHT", _clock);
        Visual summary = new TextBlock(() => _summary.Value)
            .Style(() => _failed.Value ? StraumrStyleService.RedText : StraumrStyleService.MutedBrightText)
            .Trimming(TextTrimming.EndEllipsis);

        Visual bar = StraumrSurfaceHelpers.Bar(
            new HStack(
                    new TextBlock(request.Method.Method).Style(HttpMethodFormatting.Style(request.Method))
                        .MinWidth(request.Method.Method.Length),
                    new TextBlock(RequestEditorStateModel.FromRequest(request).GetDisplayUri())
                        .Style(StraumrStyleService.PrimaryText)
                        .Trimming(TextTrimming.EndEllipsis)
                        .HorizontalAlignment(Align.Stretch))
                .Spacing(2)
                .HorizontalAlignment(Align.Stretch),
            new ComputedVisual(() => _sending.Value ? pulse : summary));

        return StraumrSurfaceHelpers.Inset(bar, ResourceScreenLayoutHelpers.PaneInset);
    }

    private Visual BuildFooter()
    {
        HintBar hints = new HintBar().Style(StraumrStyleService.CommandBar);
        return new ZStack(
                StraumrSurfaceHelpers.Inset(hints, StraumrSurfaceHelpers.RowInset)
                    .IsVisible(() => _notice.Value.Length == 0),
                StraumrSurfaceHelpers.Inset(
                        new TextBlock(() => _notice.Value)
                            .Style(() => _noticeError.Value ? StraumrStyleService.RedText : StraumrStyleService.MutedText)
                            .Trimming(TextTrimming.EndEllipsis)
                            .HorizontalAlignment(Align.Stretch),
                        StraumrSurfaceHelpers.RowInset)
                    .IsVisible(() => _notice.Value.Length > 0))
            .HorizontalAlignment(Align.Stretch);
    }

    private void AddExit(string label, Func<bool> available, Action execute) => _dialog.AddCommand(new Command
    {
        Id = $"Response.{label}",
        LabelMarkup = label,
        Gesture = TuiKeybindHelpers.Get($"Response.{label}"),
        Importance = CommandImportance.Primary,
        Presentation = CommandPresentation.CommandBar,
        CanExecute = _ => available(),
        IsVisible = _ => available(),
        ConsumesGestureWhenUnavailable = false,
        Execute = _ => execute()
    });
}
